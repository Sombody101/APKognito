#if DEBUG
// Only used for debugging (multiple threads running will disrupt stepthrough-debugging by triggering breakpoints)
//#define SINGLE_THREAD_INSTANCE_REPLACING
#endif

using APKognito.AdbTools;
using APKognito.ApkMod.Automation;
using APKognito.Configurations.ConfigModels;
using APKognito.Exceptions;
using APKognito.Models;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Pages;
using Microsoft.Extensions.Primitives;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

#if CHANGE_BINARY_NAMES

#endif

namespace APKognito.ApkMod;

public class ApkEditorContext : IProgressReporter
{
    private const int MAX_SMALI_LOAD_SIZE = 1024 * 20; // 20KB

    private Regex lineReplaceRegex = null!;

    private readonly IViewLogger logger;
    private readonly KognitoConfig kognitoConfig;

    private readonly ApkNameData nameData;

    private readonly ApkRenameSettings renameSettings;
    private readonly AdvancedApkRenameSettings advancedRenameSettings;

    public event EventHandler<ProgressUpdateEventArgs> ProgressChanged = null!;

    public string? AssetPath { get; private set; }
    public string OutputApkPath { get; private set; } = string.Empty;

    public ApkEditorContext(
        ApkRenameSettings renameSettings,
        AdvancedApkRenameSettings advancedRenameSettings,
        IViewLogger logger,
        KognitoConfig _kognitoConfig,
        bool limited = false
    )
    {
        kognitoConfig = _kognitoConfig;

        this.renameSettings = renameSettings;
        this.advancedRenameSettings = advancedRenameSettings;
        this.logger = logger;

        string sourceApkName = Path.GetFileName(renameSettings.SourceApkPath);
        if (sourceApkName.EndsWith(".apk"))
        {
            sourceApkName = sourceApkName[..^4];
        }

        if (limited)
        {
            nameData = new();
            return;
        }

        nameData = new()
        {
            FullSourceApkPath = renameSettings.SourceApkPath,
            FullSourceApkFileName = sourceApkName,
            NewCompanyName = renameSettings.ApkReplacementName,
            ApkSmaliTempDirectory = Path.Combine(renameSettings.TempDirectory, "$smali"),
            ApkAssemblyDirectory = Path.Combine(renameSettings.TempDirectory, GetFormattedTimeDirectory(sourceApkName))
        };

        // Temporary directories
        _ = Directory.CreateDirectory(nameData.ApkAssemblyDirectory);
    }

    /// <summary>
    /// Unpacks, replaces all package name occurrences with a new company name, repacks the package, then signs the package.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="RenameFailedException"></exception>
    public async Task<string?> RenameLoadedPackageAsync(CancellationToken cancellationToken)
    {
        // This method is a big mess due to the usage of Path.Combine(). I've tried to clean it up as
        // much as I can without defining so many strings.

        try
        {
            // Unpack
            logger.Log($"Unpacking {nameData.FullSourceApkFileName}");
            await UnpackApkAsync(cancellationToken);

            // Get original package name
            logger.Log("Getting package name...");
            nameData.OriginalPackageName = GetPackageName(Path.Combine(nameData.ApkAssemblyDirectory, "AndroidManifest.xml"));

            // Format new package name and get original company name
            (
                _,
                nameData.OriginalCompanyName,
                nameData.NewPackageName
            ) = SplitPackageName(nameData);

            renameSettings.OnPackageNameFound?.Invoke(nameData.NewPackageName);

            if (advancedRenameSettings.RenameLibsInternal && nameData.OriginalCompanyName.Length != nameData.NewCompanyName.Length)
            {
                throw new InvalidReplacementCompanyNameException(nameData.OriginalCompanyName, nameData.NewCompanyName);
            }

            nameData.RenamedApkOutputDirectory = Path.Combine(renameSettings.OutputDirectory, GetFormattedTimeDirectory(nameData.NewPackageName));

            if (!kognitoConfig.ClearTempFilesOnRename)
            {
                Directory.CreateDirectory(nameData.RenamedApkOutputDirectory);
                DriveUsageViewModel.ClaimDirectory(nameData.RenamedApkOutputDirectory);
            }

            // 'nameData' should not be modified after this point.

            var automationConfig = await GetParsedAutoConfigAsync();

            // Nothing happens for the unpack stage that should be saved, but we still run the commands
            _ = await GetCommandResultAsync(automationConfig, CommandStage.Unpack);

            // Replace all instances in the APK and any OBBs
            logger.Log($"Changing '{nameData.OriginalPackageName}'  |>  '{nameData.NewPackageName}'");
            lineReplaceRegex = advancedRenameSettings.BuildRegex(nameData.OriginalCompanyName);

#if SINGLE_THREAD_INSTANCE_REPLACING
            ThreadPool.SetMaxThreads(1, 1);
#endif

            await ReplaceAllNameInstancesAsync(automationConfig, cancellationToken);

            // Visit the pack stage commands as well
            _ = await GetCommandResultAsync(automationConfig, CommandStage.Pack);

            // Repack
            logger.Log("Packing APK...");
            string unsignedApk = await PackApkAsync(cancellationToken);

            // Align
            logger.Log("Aligning package...");
            await AlignPackageAsync(unsignedApk);

            // Sign
            logger.Log("Signing APK...");
            await SignApkToolAsync(unsignedApk, cancellationToken);

            // Copy to output and cleanup
            logger.Log($"Finished APK {nameData.NewPackageName}");
            logger.Log($"Placed into: {nameData.RenamedApkOutputDirectory}");
            logger.Log("Cleaning up...");

            if (kognitoConfig.ClearTempFilesOnRename)
            {
                logger.LogDebug($"Clearing temp directory `{nameData.ApkAssemblyDirectory}`");
                Directory.Delete(nameData.ApkAssemblyDirectory, true);
            }

            if (!kognitoConfig.CopyFilesWhenRenaming)
            {
                logger.LogDebug($"CopyWhenRenaming enabled, deleting directory `{nameData.FullSourceApkPath}`");

                try
                {
                    File.Delete(nameData.FullSourceApkPath);

                    string obbDirectory = Path.GetDirectoryName(nameData.FullSourceApkPath)
                        ?? throw new RenameFailedException("Failed to clean OBB directory ");

                    Directory.Delete(obbDirectory);
                }
                catch (Exception ex)
                {
                    FileLogger.LogException(ex);
                    logger.LogWarning($"Failed to clear source APK (CopyWhenRenaming=Enabled): {ex.Message}");
                }
            }

            // Claim the directory as an app so it appears in the drive footprint page
            DriveUsageViewModel.ClaimDirectory(nameData.RenamedApkOutputDirectory);
        }
        catch (OperationCanceledException)
        {
            return "Job canceled.";
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);

            // All methods called in this try/catch will not handle their own exceptions.
            // If they do, it's to reformat the error message and re-throw it to be caught here.
#if DEBUG
            // Exceptions are added to the exceptions log file, so there's no reason to add a stack trace here. It could overwhelm or distract the user if
            // they're not used to that kind of thing. Will that stop people from copying the entire LogBox control and pasting it in
            // their GitHub issue along with the logpack which already contains that information?
            // Nope.
            return $"{(ex.InnerException ?? ex).GetType().Name}: {ex.Message}\n{ex.StackTrace}";
#else
            return $"{(ex.InnerException ?? ex).GetType().Name}: {ex.Message}";
#endif
        }

        return null;
    }

    public async Task UnpackApkAsync(string apkPath, string outputDirectory, CancellationToken cToken = default)
    {
        string args = $"-jar \"{ApkEditorToolPaths.ApktoolJarPath}\" -f d \"{apkPath}\" -o \"{outputDirectory}\"";
        using Process process = CreateJavaProcess(args);

        _ = process.Start();
        await process.WaitForExitAsync(cToken);

        if (process.ExitCode is 0)
        {
            return;
        }

        string error = await process.StandardError.ReadToEndAsync(cToken);
        logger.LogError($"Failed to unpack {apkPath}. Error: {error}");
        throw new RenameFailedException(error);
    }

    private async Task UnpackApkAsync(CancellationToken cToken)
    {
        ReportUpdate("Unpacking", ProgressUpdateType.Title);
        ReportUpdate("Starting apktools...");

        // Output the unpacked APK in the temp folder
        string args = $"-jar \"{ApkEditorToolPaths.ApktoolJarPath}\" -f d \"{nameData.FullSourceApkPath}\" -o \"{nameData.ApkAssemblyDirectory}\"";
        using Process process = CreateJavaProcess(args);

        ReportUpdate($"Unpacking {nameData.FullSourceApkFileName}");
        _ = process.Start();
        await process.WaitForExitAsync(cToken);

        if (process.ExitCode is 0)
        {
            if (!kognitoConfig.CopyFilesWhenRenaming)
            {
                File.Delete(nameData.FullSourceApkPath);
            }

            return;
        }

        string error = await process.StandardError.ReadToEndAsync(cToken);
        logger.LogError($"Failed to unpack {nameData.FullSourceApkFileName}. Error: {error}");
        throw new RenameFailedException(error);
    }

    public async Task PackApkAsync(string apkDirectory, string outputFile, CancellationToken token = default)
    {
        string args = $"-jar \"{ApkEditorToolPaths.ApktoolJarPath}\" -f b \"{apkDirectory}\" -o \"{outputFile}\"";
        using Process process = CreateJavaProcess(args);

        _ = process.Start();
        await process.WaitForExitAsync(token);

        if (process.ExitCode is 0)
        {
            return;
        }

        string error = await process.StandardError.ReadToEndAsync(token);
        logger.LogError($"Failed to pack {apkDirectory}. Error: {error}");
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

        string args = $"-jar \"{ApkEditorToolPaths.ApktoolJarPath}\" -f b \"{nameData.ApkAssemblyDirectory}\" -o \"{outputApkPath}\"";
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
        _ = await AdbManager.QuickGenericCommandAsync(ApkEditorToolPaths.ZipalignPath, args);

        // c.z.a.apk.aligned -> c.z.a.apk
        File.Delete(apkPath);
        File.Move(alignedPackagePath, apkPath);
    }

    private async Task SignApkToolAsync(string apkPath, CancellationToken cToken)
    {
        ReportUpdate("Signing", ProgressUpdateType.Title);
        ReportUpdate(Path.GetFileName(apkPath));

        string args = $"-jar \"{ApkEditorToolPaths.ApksignerJarPath}\" -a \"{apkPath}\" -o \"{nameData.RenamedApkOutputDirectory}\" --allowResign";
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
        AssetPath = fullTrueName;
        OutputApkPath = $"{fullTrueName}.apk";

        File.Move($"{newSignedName}.apk", OutputApkPath, true);
        File.Move($"{newSignedName}.apk.idsig", $"{fullTrueName}.apk.idsig", true);
    }

    private async Task ReplaceAllNameInstancesAsync(AutoConfigModel? config, CancellationToken cToken)
    {
        ThreadPool.GetMaxThreads(out int maxTaskThreads, out int maxIoThreads);

        if (maxTaskThreads == 1 && maxIoThreads == 1)
        {
            logger.Log("Multi threading disabled.");
        }
        else
        {
            logger.Log($"Using {maxTaskThreads} max task threads, {maxIoThreads} max I/O threads.");
        }

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

        renameFiles = renameFiles.Concat(advancedRenameSettings.ExtraInternalPackagePaths
            .Where(p => p.FileType is FileType.RegularText)
            .Select(p => Path.Combine(nameData.ApkAssemblyDirectory, p.FilePath)));

        Directory.CreateDirectory(nameData.ApkSmaliTempDirectory);

        int workingOnFile = 0;

#if SINGLE_THREAD_INSTANCE_REPLACING
        string[] filesArr = [.. renameFiles];
        logger.LogDebug($"Beginning sequential rename on {filesArr.Length:n0} smali files.");

        foreach (string filePath in filesArr)
        {
            workingOnFile++;
            ReportUpdate(workingOnFile.ToString());

            await ReplaceTextInFileAsync(filePath, cToken);
        }
#else
        object updateLock = new();

        logger.LogDebug($"Beginning threaded rename on {renameFiles.Count():n0} smali files.");
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
        if (kognitoConfig.CopyFilesWhenRenaming)
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
        var obbArchives = Directory.EnumerateFiles(newObbDirectory, $"*{nameData.FullSourceApkFileName}.obb")
            .Concat(advancedRenameSettings.ExtraInternalPackagePaths
                .Where(p => p.FileType == FileType.Archive)
                .Select(p => Path.Combine(nameData.ApkAssemblyDirectory, p.FilePath)))
            .FilterByCommandResult(additionals);

        foreach (string filePath in obbArchives)
        {
            ReportUpdate(Path.GetFileName(filePath));
            string newAssetName = $"{Path.GetFileNameWithoutExtension(filePath).Replace(originalCompanyName, nameData.NewCompanyName)}.obb";

            logger.Log($"Renaming asset file: {Path.GetFileName(filePath)}  |>  {newAssetName}");

            if (advancedRenameSettings.RenameObbsInternal)
            {
                var binaryReplace = new BinaryReplace(filePath, logger);

                binaryReplace.ProgressChanged += (object? sender, ProgressUpdateEventArgs args) => ForwardUpdate(args);

                await binaryReplace.ModifyArchiveStringsAsync(lineReplaceRegex, nameData.NewCompanyName, [.. advancedRenameSettings.RenameObbsInternalExtras], token);
            }

            File.Move(filePath, Path.Combine(newObbDirectory, newAssetName));
        }

        string newApkName = nameData.OriginalPackageName.Replace(originalCompanyName, nameData.NewCompanyName);
        RenameDirectory(newObbDirectory, newApkName, nameData);
        AssetPath = Path.Combine(newObbDirectory, newApkName);
    }

    private async Task ReplaceTextInFileAsync(string filePath, CancellationToken cToken)
    {
        if (!File.Exists(filePath))
        {
            logger.LogWarning($"Failed to find file {ApkNameData.SubtractPathFrom(nameData.ApkAssemblyDirectory, filePath)}");
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

        string Replace(string original)
        {
            return lineReplaceRegex.Replace(original, nameData.NewCompanyName);
        }
    }

    private async Task ReplaceLibInstancesAsync(CommandStageResult? additionals, CancellationToken token)
    {
        string libs = Path.Combine(nameData.ApkAssemblyDirectory, "lib");
        if (!Directory.Exists(libs))
        {
            logger.Log("No libs found. Not renaming binaries.");
            return;
        }

        if (!advancedRenameSettings.RenameLibs)
        {
            return;
        }

        ReportUpdate("Renaming libraries", ProgressUpdateType.Title);

        var elfBinaries = Directory.EnumerateFiles(libs, "*.so", SearchOption.AllDirectories)
            .Concat(advancedRenameSettings.ExtraInternalPackagePaths
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
            string newFileName = advancedRenameSettings.RenameLibs
                ? lineReplaceRegex.Replace(originalName, nameData.NewCompanyName)
                : originalName;

            string newFilePath = Path.Combine(Path.GetDirectoryName(originalFilePath)!, newFileName);

            // The actual rename action has to be deferred to prevent access exceptions :p
            logger.Log($"Renaming lib file: {originalName}{(originalName != newFileName ? $" |> {newFileName}" : string.Empty)}");

            if (advancedRenameSettings.RenameLibsInternal)
            {
                logger.AddIndent();

                var binaryReplace = new BinaryReplace(originalFilePath, logger);

                binaryReplace.ProgressChanged += (object? sender, ProgressUpdateEventArgs args) => ForwardUpdate(args);

                await binaryReplace.ModifyElfStringsAsync(lineReplaceRegex, nameData.NewCompanyName, token);

                logger.ResetIndent();
            }

            ReportUpdate(newFilePath);
            File.Move(originalFilePath, newFilePath);
        }
    }

    private Process CreateJavaProcess(string arguments)
    {
        return new()
        {
            StartInfo = new()
            {
                FileName = renameSettings.JavaPath,
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
        if (!advancedRenameSettings.AutoPackageEnabled)
        {
            return null;
        }

        if (advancedRenameSettings.AutoPackageConfig is null)
        {
            logger.LogWarning("Found auto config is null (won't be parsed).");
            return null;
        }

        using MemoryStream configStream = new(Encoding.Default.GetBytes(advancedRenameSettings.AutoPackageConfig));
        using StreamReader streamReader = new(configStream);

        // We all know damn well this is not compiling, but "parsing" didn't sound as cool :p
        logger.Log("Compiling auto configuration...");

        var parser = new ConfigParser(streamReader);
        return await parser.BeginParseAsync();
    }

    private async Task<CommandStageResult?> GetCommandResultAsync(AutoConfigModel? config, CommandStage stage)
    {
        if (config is null)
        {
            return null;
        }

        var foundStage = config.GetStage(stage);

        if (foundStage is null)
        {
            logger.LogDebug($"No stage found for {stage}, no alterations made.");
            return null;
        }

        logger.Log("-- Entering auto configuration script.");
        logger.AddIndentString("[SCRIPT]: ");

        try
        {
            return await new CommandDispatcher(foundStage, nameData.ApkAssemblyDirectory, new()
            {
                { "originalCompany", nameData.OriginalCompanyName },
                { "originalPackage", nameData.OriginalPackageName },
                { "newCompany", nameData.NewCompanyName },
                { "newPackage", nameData.NewPackageName },
            }, logger)
                .DispatchCommandsAsync();
        }
        finally
        {
            logger.ResetIndent();
            logger.Log("-- Exiting auto configuration script.");
        }
    }

    private static void RenameDirectory(string originalDirectory, string newName, ApkNameData nameData)
    {
        if (Path.GetFileName(originalDirectory) == newName)
        {
            // The name already matches
            return;
        }

        string trimmedDirectory = originalDirectory.Length > nameData.ApkAssemblyDirectory.Length
            ? originalDirectory[nameData.ApkAssemblyDirectory.Length..]
            : originalDirectory;

        FileLogger.Log($"Changing .{trimmedDirectory} -> {newName}");

        string newFolderPath = Path.Combine(Path.GetDirectoryName(originalDirectory)!, newName);

        if (Directory.Exists(newFolderPath))
        {
            FileLogger.LogWarning($"The directory '{trimmedDirectory}' has already been renamed to {newName}, skipping.");
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

        _ = await Task.Run(() => Directory.CreateDirectory(destinationDir)); // Offload synchronous operation

        List<FileInfo> files = await Task.Run(() => dir.GetFiles().ToList()); // Offload synchronous operation

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
            List<DirectoryInfo> subDirectories = await Task.Run(() => dir.GetDirectories().ToList()); // Offload synchronous operation
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

    private static string GetFormattedTimeDirectory(string sourceApkName)
    {
        return $"{sourceApkName}_{DateTime.Now:yyyy-MMMM-dd_h.mm}";
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
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            return 0;
        }
    }

    public void ReportUpdate(string update, ProgressUpdateType updateType = ProgressUpdateType.Content)
    {
        ProgressChanged?.Invoke(this, new(update, updateType));
    }

    public void ForwardUpdate(ProgressUpdateEventArgs args)
    {
        ProgressChanged?.Invoke(this, args);
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
        public string NewCompanyName { get; init; } = string.Empty;

        /// <summary>
        /// The full path to the source APK file.
        /// </summary>
        public string FullSourceApkPath { get; init; } = string.Empty;

        /// <summary>
        /// The full name for the source APK file.
        /// </summary>
        public string FullSourceApkFileName { get; init; } = string.Empty;

        /// <summary>
        /// The final directory that the renamed APK is placed. (Passed from caller)
        /// </summary>
        public string RenamedApkOutputDirectory { get; set; } = string.Empty;

        /// <summary>
        /// The temporary directory that the unpacked APK is placed.
        /// </summary>
        public string ApkAssemblyDirectory { get; init; } = string.Empty;

        /// <summary>
        /// A sub-directory in side of <see cref="ApkAssemblyDirectory"/> for replacing company name instances.
        /// (Only used when file is larger than <see cref="MAX_SMALI_LOAD_SIZE"/>)
        /// </summary>
        public string ApkSmaliTempDirectory { get; init; } = string.Empty;

        public static string SubtractPathFrom(string path, string subtractor)
        {
            if (!path.StartsWith(subtractor))
            {
                return path;
            }

            return path[subtractor.Length..];
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