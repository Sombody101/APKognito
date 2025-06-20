#if DEBUG
// Only used for debugging (multiple threads running will disrupt stepthrough-debugging by triggering breakpoints)
//#define SINGLE_THREAD_INSTANCE_REPLACING
#endif

using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using APKognito.ApkLib.Exceptions;
using APKognito.Legacy.ApkLib.Automation;
using APKognito.Legacy.ApkLib.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace APKognito.Legacy.ApkLib;

public class ApkEditorContext
{
    private const int MAX_SMALI_LOAD_SIZE = 1024 * 20; // 20KB

    private Regex lineReplaceRegex = null!;

    private readonly ILogger logger;

    private readonly ApkNameData nameData;

    private readonly ApkRenameSettings renameSettings;

    private readonly IProgress<ProgressInfo>? progressReporter;

    private string? assetPath;
    private string outputApkPath = string.Empty;

    public ApkEditorContext(
        ApkRenameSettings renameSettings,
        IProgress<ProgressInfo>? progressReporter,
        ILogger logger
    )
    {
        ArgumentNullException.ThrowIfNull(renameSettings);
        ArgumentNullException.ThrowIfNull(logger);

        this.renameSettings = renameSettings;
        this.progressReporter = progressReporter;
        this.logger = logger;

        string sourceApkName = Path.GetFileName(renameSettings.SourceApkPath);
        if (sourceApkName.EndsWith(".apk"))
        {
            sourceApkName = sourceApkName[..^4];
        }

        nameData = new()
        {
            FullSourceApkPath = renameSettings.SourceApkPath,
            FullSourceApkFileName = sourceApkName,
            NewCompanyName = renameSettings.ApkReplacementName,
            ApkSmaliTempDirectory = Path.Combine(renameSettings.TempDirectory, "$smali"),
            ApkAssemblyDirectory = renameSettings.TempDirectory
        };

        // Temporary directories
        _ = Directory.CreateDirectory(nameData.ApkAssemblyDirectory);
    }

    public ApkEditorContext(ApkRenameSettings? settings, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        renameSettings = settings;
        this.logger = logger;

        string sourceApkName = Path.GetFileName(renameSettings.SourceApkPath);
        if (sourceApkName.EndsWith(".apk"))
        {
            sourceApkName = sourceApkName[..^4];
        }

        nameData = new()
        {
            FullSourceApkPath = renameSettings.SourceApkPath,
            FullSourceApkFileName = sourceApkName,
            NewCompanyName = renameSettings.ApkReplacementName,
            ApkSmaliTempDirectory = Path.Combine(renameSettings.TempDirectory, "$smali"),
            ApkAssemblyDirectory = renameSettings.TempDirectory
        };
    }

    /// <summary>
    /// Unpacks, replaces all package name occurrences with a new company name, repacks the package, then signs the package.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="RenameFailedException"></exception>
    public async Task<PackageRenameResult> RenameLoadedPackageAsync(CancellationToken token = default)
    {
        // This method is a big mess due to the usage of Path.Combine(). I've tried to clean it up as
        // much as I can without defining so many strings.

        ArgumentException.ThrowIfNullOrWhiteSpace(renameSettings.ApksignerJarPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(renameSettings.ApktoolBatPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(renameSettings.ApksignerJarPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(renameSettings.ZipalignPath);

        try
        {
            await RunRenameInternalAsync(token);
        }
        catch (OperationCanceledException)
        {
            return new()
            {
                ResultStatus = "Job canceled.",
                Successful = false,
                OutputLocations = new(assetPath, outputApkPath)
            };
        }
        catch (Exception ex)
        {
            ex = ex.InnerException ?? ex;

            logger.LogError("{Message}", ex.Message);
#if DEBUG
            logger.LogDebug("{StackTrace}", ex.StackTrace);
#endif

            string result = $"{ex.GetType().Name}: {ex.Message}";
            return new PackageRenameResult()
            {
                ResultStatus = result,
                Successful = false,
                OutputLocations = new(assetPath, outputApkPath)
            };
        }

        return new()
        {
            ResultStatus = "Successful",
            Successful = true,
            OutputLocations = new(assetPath, outputApkPath)
        };
    }

    private async Task RunRenameInternalAsync(CancellationToken token = default)
    {
        // Unpack
        logger.LogInformation("Unpacking {FullSourceApkFileName}", nameData.FullSourceApkFileName);
        await UnpackApkAsync(token);

        // Get original package name
        logger.LogInformation("Getting package name...");
        nameData.OriginalPackageName = GetPackageName(Path.Combine(nameData.ApkAssemblyDirectory, "AndroidManifest.xml"));

        // Format new package name and get original company name
        (
            _,
            nameData.OriginalCompanyName,
            nameData.NewPackageName
        ) = SplitPackageName(nameData);

        if (renameSettings.OutputDirectory is null)
        {
            ArgumentNullException.ThrowIfNull(renameSettings.OutputBaseDirectory);
            nameData.RenamedApkOutputDirectory = Path.Combine(renameSettings.OutputBaseDirectory, GetFormattedTimeDirectory(nameData.NewPackageName));
        }
        else
        {
            nameData.RenamedApkOutputDirectory = renameSettings.OutputDirectory;
        }

        // 'nameData' should not be modified after this point.

        if (renameSettings.RenameLibsInternal && nameData.OriginalCompanyName.Length != nameData.NewCompanyName.Length)
        {
            throw new InvalidReplacementCompanyNameException(nameData.OriginalCompanyName, nameData.NewCompanyName);
        }

        if (!renameSettings.ClearTempFilesOnRename)
        {
            _ = Directory.CreateDirectory(nameData.RenamedApkOutputDirectory);
        }

        AutoConfigModel? automationConfig = await GetParsedAutoConfigAsync();

        // Nothing happens for the unpack stage that should be saved, but we still run the commands
        _ = await GetCommandResultAsync(automationConfig, CommandStage.Unpack);

        // Replace all instances in the APK and any OBBs
        logger.LogInformation("Changing '{OriginalPackageName}'  |>  '{NewPackageName}'", nameData.OriginalPackageName, nameData.NewPackageName);
        lineReplaceRegex = renameSettings.BuildRegex(nameData.OriginalCompanyName);

        await ReplaceAllNameInstancesAsync(automationConfig, token);

        // Visit the pack stage commands as well
        _ = await GetCommandResultAsync(automationConfig, CommandStage.Pack);

        // Repack
        logger.LogInformation("Packing APK...");
        string unsignedApk = await PackApkAsync(token);

        // Align
        logger.LogInformation("Aligning package...");
        await AlignPackageAsync(unsignedApk);

        // Sign
        logger.LogInformation("Signing APK...");
        await SignApkToolAsync(unsignedApk, token);

        // Copy to output and cleanup
        logger.LogInformation("Finished APK {NewPackageName}", nameData.NewPackageName);
        logger.LogInformation("Placed into: {RenamedApkOutputDirectory}", nameData.RenamedApkOutputDirectory);
        logger.LogInformation("Cleaning up...");

        if (renameSettings.ClearTempFilesOnRename)
        {
            logger.LogDebug("Clearing temp directory `{ApkAssemblyDirectory}`", nameData.ApkAssemblyDirectory);
            Directory.Delete(nameData.ApkAssemblyDirectory, true);
        }

        if (!renameSettings.CopyFilesWhenRenaming)
        {
            logger.LogDebug("CopyWhenRenaming disabled, deleting directory `{FullSourceApkPath}`", nameData.FullSourceApkPath);

            try
            {
                File.Delete(nameData.FullSourceApkPath);

                string obbDirectory = Path.GetDirectoryName(nameData.FullSourceApkPath)
                    ?? throw new RenameFailedException("Failed to clean OBB directory ");

                Directory.Delete(obbDirectory);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to clear source APK.");
            }
        }
    }

    public async Task UnpackApkAsync(string apkPath, string outputDirectory, CancellationToken cToken = default)
    {
        string args = $"-jar \"{renameSettings.ApktoolJarPath}\" -f d \"{apkPath}\" -o \"{outputDirectory}\"";
        using Process process = CreateJavaProcess(args);

        _ = process.Start();
        await process.WaitForExitAsync(cToken);

        if (process.ExitCode is 0)
        {
            return;
        }

        string error = await process.StandardError.ReadToEndAsync(cToken);
        logger.LogError("Failed to unpack {ApkPath}. Error: {Error}", apkPath, error);
        throw new RenameFailedException(error);
    }

    private async Task UnpackApkAsync(CancellationToken cToken)
    {
        ReportUpdate("Unpacking", ProgressUpdateType.Title);
        ReportUpdate("Starting apktools...");

        // Output the unpacked APK in the temp folder
        string args = $"-jar \"{renameSettings.ApktoolJarPath}\" -f d \"{nameData.FullSourceApkPath}\" -o \"{nameData.ApkAssemblyDirectory}\"";
        using Process process = CreateJavaProcess(args);

        ReportUpdate($"Unpacking {nameData.FullSourceApkFileName}");
        _ = process.Start();
        await process.WaitForExitAsync(cToken);

        if (process.ExitCode is 0)
        {
            if (!renameSettings.CopyFilesWhenRenaming)
            {
                File.Delete(nameData.FullSourceApkPath);
            }

            return;
        }

        string error = await process.StandardError.ReadToEndAsync(cToken);
        logger.LogError("Failed to unpack {FullSourceApkFileName}. Error: {Error}", nameData.FullSourceApkFileName, error);
        throw new RenameFailedException(error);
    }

    public async Task PackApkAsync(string apkDirectory, string outputFile, CancellationToken token = default)
    {
        string args = $"-jar \"{renameSettings.ApktoolJarPath}\" -f b \"{apkDirectory}\" -o \"{outputFile}\"";
        using Process process = CreateJavaProcess(args);

        _ = process.Start();
        await process.WaitForExitAsync(token);

        if (process.ExitCode is 0)
        {
            return;
        }

        string error = await process.StandardError.ReadToEndAsync(token);
        logger.LogError("Failed to pack {ApkDirectory}. Error: {Error}", apkDirectory, error);
        throw new RenameFailedException(error);
    }

    private async Task<string> PackApkAsync(CancellationToken cToken)
    {
        ReportUpdate("Packing package", ProgressUpdateType.Title);
        ReportUpdate(nameData.NewPackageName);

        string outputApkPath = Path.Combine(
            nameData.ApkAssemblyDirectory,
            $"{nameData.NewPackageName}.unsigned.apk"
        );

        string args = $"-jar \"{renameSettings.ApktoolJarPath}\" -f b \"{nameData.ApkAssemblyDirectory}\" -o \"{outputApkPath}\"";
        using Process process = CreateJavaProcess(args);

        _ = process.Start();
        await process.WaitForExitAsync(cToken);

        return process.ExitCode is not 0
            ? throw new RenameFailedException(await process.StandardError.ReadToEndAsync(cToken))
            : outputApkPath;
    }

    private async Task AlignPackageAsync(string apkPath)
    {
        ReportUpdate("Aligning", ProgressUpdateType.Title);

        string alignedPackagePath = $"{apkPath}.aligned";
        ReportUpdate(Path.GetFileName(alignedPackagePath));

        string args = $"-f -p 4 \"{apkPath}\" \"{alignedPackagePath}\"";

        Process alignProcess = CreateJavaProcess(args, renameSettings.ZipalignPath);

        _ = alignProcess.Start();
        await alignProcess.WaitForExitAsync();

        // c.z.a.apk.aligned -> c.z.a.apk
        File.Delete(apkPath);
        File.Move(alignedPackagePath, apkPath);
    }

    private async Task SignApkToolAsync(string apkPath, CancellationToken cToken)
    {
        ReportUpdate("Signing", ProgressUpdateType.Title);
        ReportUpdate(Path.GetFileName(apkPath));

        string args = $"-jar \"{renameSettings.ApksignerJarPath}\" -a \"{apkPath}\" -o \"{nameData.RenamedApkOutputDirectory}\" --allowResign";
        using Process process = CreateJavaProcess(args);

        _ = process.Start();
        await process.WaitForExitAsync(cToken);

        string error = await process.StandardError.ReadToEndAsync(cToken);

        if (process.ExitCode is not 0)
        {
            throw new RenameFailedException(error);
        }

        // Rename the output APK
        // fullTrueName is also the OBB asset path when it doesn't have the file extension
        string fullTrueName = Path.Combine(nameData.RenamedApkOutputDirectory, Path.GetFileName(apkPath).Replace(".unsigned.apk", string.Empty));
        string newSignedName = $"{fullTrueName}.unsigned-aligned-debugSigned";

        // Paths for pushing to a device
        assetPath = fullTrueName;
        outputApkPath = $"{fullTrueName}.apk";

        File.Move($"{newSignedName}.apk", outputApkPath, true);
        File.Move($"{newSignedName}.apk.idsig", $"{fullTrueName}.apk.idsig", true);
    }

    private async Task ReplaceAllNameInstancesAsync(AutoConfigModel? config, CancellationToken cToken)
    {
        ReplaceAllDirectoryNames(
            await GetCommandResultAsync(config, CommandStage.Directory),
            nameData.ApkAssemblyDirectory
        );

        await ReplaceLibInstancesAsync(
            await GetCommandResultAsync(config, CommandStage.Library),
            cToken
        );

        await RenameSmaliFilesAsync(
            await GetCommandResultAsync(config, CommandStage.Smali),
            cToken
        );

        // This will take the most disk space, so do it only if the smali renaming was successful
        // There should definitely be an option in the future to just push these files to a headset under a new name (as long as the assets don't need to be edited)
        await RenameObbFilesAsync(
            await GetCommandResultAsync(config, CommandStage.Assets),
            cToken
        );
    }

    private async Task RenameSmaliFilesAsync(CommandStageResult? additionals, CancellationToken cToken)
    {
        ReportUpdate("Renaming directories", ProgressUpdateType.Title);
        ReportUpdate(string.Empty);

        string smaliDirectory = Path.Combine(nameData.ApkAssemblyDirectory, "smali");
        IEnumerable<string> renameFiles = Directory.EnumerateFiles(smaliDirectory, "*.smali", SearchOption.AllDirectories)
            .Append($"{nameData.ApkAssemblyDirectory}\\AndroidManifest.xml")
            .Append($"{nameData.ApkAssemblyDirectory}\\apktool.yml")
            .FilterByCommandResult(additionals);

        foreach (string directory in Directory.GetDirectories(nameData.ApkAssemblyDirectory, "smali_*"))
        {
            renameFiles = renameFiles.Concat(Directory.EnumerateFiles(directory, "*.smali", SearchOption.AllDirectories));
        }

        string libDirectory = Path.Combine(nameData.ApkAssemblyDirectory, "lib");
        if (Directory.Exists(libDirectory))
        {
            renameFiles = Directory.EnumerateFiles(libDirectory, "*.config.so", SearchOption.AllDirectories)
                .Concat(renameFiles);
        }

        renameFiles = renameFiles.Concat(renameSettings.ExtraInternalPackagePaths
            .Where(p => p.FileType is FileType.RegularText)
            .Select(p => Path.Combine(nameData.ApkAssemblyDirectory, p.FilePath)));

        Directory.CreateDirectory(nameData.ApkSmaliTempDirectory);

        int workingOnFile = 0;

#if SINGLE_THREAD_INSTANCE_REPLACING
        string[] filesArr = [.. renameFiles];
        logger.LogInformationDebug($"Beginning sequential rename on {filesArr.Length:n0} smali files.");

        foreach (string filePath in filesArr)
        {
            workingOnFile++;
            ReportUpdate(workingOnFile.ToString());

            await ReplaceTextInFileAsync(filePath, cToken);
        }
#else
        object updateLock = new();

        logger.LogDebug("Beginning threaded rename on {Count:n0} smali files.", renameFiles.Count());
        ReportUpdate("Renaming file", ProgressUpdateType.Title);

        await Parallel.ForEachAsync(renameFiles, cToken,
            async (filePath, subcToken) =>
            {
                Interlocked.Increment(ref workingOnFile);

                if (Monitor.TryEnter(updateLock))
                {
                    ReportUpdate(workingOnFile.ToString());
                }

                await ReplaceTextInFileAsync(filePath, subcToken);
            }
        );
#endif
    }

    private async Task RenameObbFilesAsync(CommandStageResult? additionals, CancellationToken token)
    {
        string originalCompanyName = nameData.OriginalCompanyName;
        string? sourceDirectory = Path.GetDirectoryName(nameData.FullSourceApkPath);

        if (sourceDirectory is null)
        {
            logger.LogError("Failed to get APK source directory. No OBB files will be renamed even if they're present.");
            return;
        }

        string sourceObbDirectory = Path.Combine(sourceDirectory, nameData.OriginalPackageName);
        if (!Directory.Exists(sourceObbDirectory))
        {
            return;
        }

        // Move/copy OBB files
        ReportUpdate(sourceDirectory);

        string newObbDirectory = Path.Combine(nameData.RenamedApkOutputDirectory, Path.GetFileName(sourceObbDirectory));
        if (renameSettings.CopyFilesWhenRenaming)
        {
            ReportUpdate("Copying OBBs", ProgressUpdateType.Title);

            await CopyDirectoryAsync(sourceObbDirectory, newObbDirectory, true);
        }
        else
        {
            ReportUpdate("Moving OBBs", ProgressUpdateType.Title);

            _ = Directory.CreateDirectory(nameData.RenamedApkOutputDirectory);
            Directory.Move(sourceObbDirectory, newObbDirectory);
        }

        // Rename the files
        IEnumerable<string> obbArchives = Directory.EnumerateFiles(newObbDirectory, $"*{nameData.FullSourceApkFileName}.obb")
            .Concat(renameSettings.ExtraInternalPackagePaths
                .Where(p => p.FileType == FileType.Archive)
                .Select(p => Path.Combine(nameData.ApkAssemblyDirectory, p.FilePath)))
            .FilterByCommandResult(additionals);

        foreach (string filePath in obbArchives)
        {
            ReportUpdate(Path.GetFileName(filePath));
            string newAssetName = $"{Path.GetFileNameWithoutExtension(filePath).Replace(originalCompanyName, nameData.NewCompanyName)}.obb";

            logger.LogInformation("Renaming asset file: {GetFileName}  |>  {NewAssetName}", Path.GetFileName(filePath), newAssetName);

            if (renameSettings.RenameObbsInternal)
            {
                var binaryReplace = new BinaryReplace(filePath, progressReporter, logger);

                await binaryReplace.ModifyArchiveStringsAsync(lineReplaceRegex, nameData.NewCompanyName, [.. renameSettings.RenameObbsInternalExtras], token);
            }

            File.Move(filePath, Path.Combine(newObbDirectory, newAssetName));
        }

        string newApkName = nameData.OriginalPackageName.Replace(originalCompanyName, nameData.NewCompanyName);
        RenameDirectory(newObbDirectory, newApkName, nameData);
        assetPath = Path.Combine(newObbDirectory, newApkName);
    }

    private async Task ReplaceTextInFileAsync(string filePath, CancellationToken cToken)
    {
        if (!File.Exists(filePath))
        {
            logger.LogWarning("Failed to find file {SubtractPathFrom}", ApkNameData.SubtractPathFrom(nameData.ApkAssemblyDirectory, filePath));
            return;
        }

        FileInfo fileInfo = new(filePath);

        if (fileInfo.Length < MAX_SMALI_LOAD_SIZE)
        {
            string content = await File.ReadAllTextAsync(filePath, cToken);
            string newContent = Replace(content);
            await File.WriteAllTextAsync(filePath, newContent, cToken);
            return;
        }

        string tempSmaliFile = Path.Combine(nameData.ApkSmaliTempDirectory, $"${fileInfo.Name}_{Random.Shared.Next():x}");
        using StreamReader reader = new(fileInfo.FullName);
        using StreamWriter writer = new(tempSmaliFile);

        string? line;
        while ((line = await reader.ReadLineAsync(cToken)) is not null)
        {
            if (!string.IsNullOrEmpty(line)
                && line.Length >= nameData.OriginalCompanyName.Length
                && !line.StartsWith('#'))
            {
                line = Replace(line);
            }

            await writer.WriteLineAsync(line);
        }

        reader.Close();
        writer.Close();

        File.Delete(fileInfo.FullName);
        File.Move(tempSmaliFile, fileInfo.FullName);

        string Replace(string original) => lineReplaceRegex.Replace(original, nameData.NewCompanyName);
    }

    private async Task ReplaceLibInstancesAsync(CommandStageResult? additionals, CancellationToken token)
    {
        string libs = Path.Combine(nameData.ApkAssemblyDirectory, "lib");
        if (!Directory.Exists(libs))
        {
            logger.LogInformation("No libs found. Not renaming binaries.");
            return;
        }

        if (!renameSettings.RenameLibs)
        {
            return;
        }

        ReportUpdate("Renaming libraries", ProgressUpdateType.Title);

        IEnumerable<string> elfBinaries = Directory.EnumerateFiles(libs, "*.so", SearchOption.AllDirectories)
            .Concat(renameSettings.ExtraInternalPackagePaths
                .Where(p => p.FileType is FileType.Elf)
                .Select(p => Path.Combine(nameData.ApkAssemblyDirectory, p.FilePath)))
            .FilterByCommandResult(additionals);

        foreach (string originalFilePath in elfBinaries)
        {
            if (originalFilePath.EndsWith(".config.so"))
            {
                continue;
            }

            string originalName = Path.GetFileName(originalFilePath);
            string newFileName = renameSettings.RenameLibs
                ? lineReplaceRegex.Replace(originalName, nameData.NewCompanyName)
                : originalName;

            string newFilePath = Path.Combine(Path.GetDirectoryName(originalFilePath)!, newFileName);

            string formattedOptionalReplacement = originalName != newFileName
                ? $" |> {newFileName}"
                : string.Empty;

            // The actual rename action has to be deferred to prevent access exceptions :p
            logger.LogInformation("Renaming lib file: {OriginalName}{FormattedOptionalReplacement}", originalName, formattedOptionalReplacement);

            if (renameSettings.RenameLibsInternal)
            {
                using IDisposable? scope = logger.BeginScope('\t');

                var binaryReplace = new BinaryReplace(originalFilePath, progressReporter, logger);

                await binaryReplace.ModifyElfStringsAsync(lineReplaceRegex, nameData.NewCompanyName, token);
            }

            ReportUpdate(newFilePath);
            File.Move(originalFilePath, newFilePath);
        }
    }

    private Process CreateJavaProcess(string arguments, string? filename = null)
    {
        return new()
        {
            StartInfo = new()
            {
                FileName = filename ?? renameSettings.JavaPath,
                Arguments = arguments,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
    }

    private void ReplaceAllDirectoryNames(CommandStageResult? additionals, string baseDirectory)
    {
        logger.LogDebug("Renaming smali directories.");

        IEnumerable<string> ienDirs = Directory.GetDirectories(baseDirectory, $"*{nameData.OriginalCompanyName}*", SearchOption.AllDirectories)
            .FilterByCommandResult(additionals)
            // Organize them to prevent "race conditions", which happens when a parent directory is renamed before a child directory, thereby throwing a DirectoryNotFoundException.
            .OrderByDescending(s => s.Length);

        ReportUpdate("Renaming directory", ProgressUpdateType.Title);

#if DEBUG
        // Not good for memory when dealing with a lot of directories.
        string[] dirs = [.. ienDirs];
        foreach (string directory in dirs)
#else
        foreach (string directory in ienDirs)
#endif
        {
            string directoryName = Path.GetFileName(directory);
            string adjustedDirectoryName = directoryName == nameData.OriginalCompanyName
                ? nameData.NewCompanyName
                : lineReplaceRegex.Replace(directoryName, nameData.NewCompanyName);

            ReportUpdate(directoryName);
            RenameDirectory(directory, adjustedDirectoryName, nameData);
        }
    }

    private async Task<AutoConfigModel?> GetParsedAutoConfigAsync()
    {
        if (!renameSettings.AutoPackageEnabled)
        {
            return null;
        }

        if (renameSettings.AutoPackageConfig is null)
        {
            logger.LogWarning("Found auto config is null (won't be parsed).");
            return null;
        }

        using MemoryStream configStream = new(Encoding.Default.GetBytes(renameSettings.AutoPackageConfig));
        using StreamReader streamReader = new(configStream);

        // We all know damn well this is not compiling, but "parsing" didn't sound as cool :p
        logger.LogInformation("Compiling auto configuration...");

        var parser = new ConfigParser(streamReader);
        return await parser.BeginParseAsync();
    }

    private async Task<CommandStageResult?> GetCommandResultAsync(AutoConfigModel? config, CommandStage stage)
    {
        if (config is null)
        {
            return null;
        }

        RenameStage? foundStage = config.GetStage(stage);

        if (foundStage is null)
        {
            logger.LogDebug("No stage found for {Stage}, no alterations made.", stage);
            return null;
        }

        logger.LogInformation("-- Entering auto configuration script for stage {Stage}.", stage);

        try
        {
            using IDisposable? scope = logger.BeginScope("[SCRIPT]");
            return await new CommandDispatcher(foundStage, nameData.ApkAssemblyDirectory, new()
            {
                { "originalCompany", nameData.OriginalCompanyName },
                { "originalPackage", nameData.OriginalPackageName },
                { "newCompany", nameData.NewCompanyName },
                { "newPackage", nameData.NewPackageName },
            }, logger).DispatchCommandsAsync();
        }
        finally
        {
            logger.LogInformation("-- Exiting auto configuration script for stage {Stage}.", stage);
        }
    }

    private void RenameDirectory(string originalDirectory, string newName, ApkNameData nameData)
    {
        if (Path.GetFileName(originalDirectory) == newName)
        {
            // The name already matches
            return;
        }

        string trimmedDirectory = originalDirectory.Length > nameData.ApkAssemblyDirectory.Length && originalDirectory.StartsWith(nameData.ApkAssemblyDirectory)
            ? originalDirectory[nameData.ApkAssemblyDirectory.Length..]
            : originalDirectory;

        logger.LogDebug("Changing .{TrimmedDirectory} -> {NewName}", trimmedDirectory, newName);

        string newFolderPath = Path.Combine(Path.GetDirectoryName(originalDirectory)!, newName);

        if (Directory.Exists(newFolderPath))
        {
            logger.LogInformation("The directory '{TrimmedDirectory}' has already been renamed to {NewName}, skipping.", trimmedDirectory, newName);
            return;
        }

        Directory.Move(originalDirectory, newFolderPath);
    }

    private static async Task CopyDirectoryAsync(string sourceDir, string destinationDir, bool recursive = false)
    {
        DirectoryInfo dir = new(sourceDir);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
        }

        _ = await Task.Run(() => Directory.CreateDirectory(destinationDir));

        List<FileInfo> files = await Task.Run(() => dir.GetFiles().ToList());

        IEnumerable<Task> copyTasks = files.Select(async file =>
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            using FileStream sourceStream = new(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            using FileStream destinationStream = new(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);

            await sourceStream.CopyToAsync(destinationStream);
        });

        await Task.WhenAll(copyTasks);

        if (recursive)
        {
            List<DirectoryInfo> subDirectories = await Task.Run(() => dir.GetDirectories().ToList());
            foreach (DirectoryInfo subDir in subDirectories)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                await CopyDirectoryAsync(subDir.FullName, newDestinationDir, true);
            }
        }
    }

    public static string GetPackageName(string manifestPath)
    {
        using FileStream stream = File.OpenRead(manifestPath);
        return GetPackageName(stream);
    }

    public static string GetPackageName(Stream fileStream)
    {
        XmlDocument xmlDoc = new();
        xmlDoc.Load(fileStream);

        return xmlDoc.DocumentElement?.Attributes["package"]?.Value
            ?? throw new RenameFailedException("Failed to get package name from AndroidManifest (XML).");
    }

    private static (string, string, string) SplitPackageName(ApkNameData nameData)
    {
        string[] split = nameData.OriginalPackageName.Split('.');

        /*
         * app => app
         * com.app => app
         * com.company.app => company
         * com.company.app.something... => company
         */
        string oldCompanyName = split.Length switch
        {
            1 => split[0],
            _ => split[1],
        };

        // Prefix, old company name, new package name
        return (split[0], oldCompanyName, nameData.OriginalPackageName.Replace(oldCompanyName, nameData.NewCompanyName));
    }

    public static long CalculateUnpackedApkSize(string apkPath, bool copyingFile = true)
    {
        try
        {
            long estimatedUnpackedSize = 0;

            using (ZipArchive archive = ZipFile.OpenRead(apkPath))
            {
                estimatedUnpackedSize = archive.Entries.Sum(entry => entry.Length);
            }

            if (!copyingFile)
            {
                // The source APK is deleted after being renamed
                estimatedUnpackedSize -= new FileInfo(apkPath).Length;
            }

            return estimatedUnpackedSize;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static string GetFormattedTimeDirectory(string sourceApkName)
    {
        return $"{sourceApkName}_{DateTime.Now:yyyy-MMMM-dd_h.mm}";
    }

    public void ReportUpdate(string update, ProgressUpdateType updateType = ProgressUpdateType.Content)
    {
        progressReporter?.Report(new(update, updateType));
    }

    /// <summary>
    /// This is to give the illusion of organization.
    /// </summary>
    private sealed class ApkNameData
    {
        /// <summary>
        /// The original full package name, fetched from the AndroidManifest.xml of the APK.
        /// </summary>
        public string OriginalPackageName { get; set; } = string.Empty;

        /// <summary>
        /// The original company name, extracted from <see cref="OriginalPackageName"/>.
        /// </summary>
        public string OriginalCompanyName { get; set; } = string.Empty;

        /// <summary>
        /// The new fully-formatted package name, using <see cref="NewCompanyName"/>.
        /// </summary>
        public string NewPackageName { get; set; } = string.Empty;

        /// <summary>
        /// The replacement package company name. (Passed from caller)
        /// </summary>
        public required string NewCompanyName { get; init; } = string.Empty;

        /// <summary>
        /// The full path to the source APK file.
        /// </summary>
        public required string FullSourceApkPath { get; init; } = string.Empty;

        /// <summary>
        /// The full name for the source APK file.
        /// </summary>
        public required string FullSourceApkFileName { get; init; } = string.Empty;

        /// <summary>
        /// The final directory that the renamed APK is placed. (Passed from caller)
        /// </summary>
        public string RenamedApkOutputDirectory { get; set; } = string.Empty;

        /// <summary>
        /// The temporary directory that the unpacked APK is placed.
        /// </summary>
        public required string ApkAssemblyDirectory { get; init; } = string.Empty;

        /// <summary>
        /// A sub-directory in side of <see cref="ApkAssemblyDirectory"/> for replacing company name instances.
        /// (Only used when file is larger than <see cref="MAX_SMALI_LOAD_SIZE"/>)
        /// </summary>
        public required string ApkSmaliTempDirectory { get; init; } = string.Empty;

        public static string SubtractPathFrom(string path, string subtractor)
        {
            return path.StartsWith(subtractor)
                ? path[subtractor.Length..]
                : path;
        }
    }

    public class InvalidReplacementCompanyNameException : Exception
    {
        public InvalidReplacementCompanyNameException(string original, string replacement)
            : base($"The replacement company name '{replacement}' is {replacement.Length} characters long when it needs to be {original.Length} characters long.")
        {
        }
    }
}
