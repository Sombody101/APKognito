﻿#if DEBUG
// Only used for debugging (multiple threads running will disrupt stepthrough-debugging by triggering breakpoints)
// #define SINGLE_THREAD_INSTANCE_REPLACING

#define CHANGE_BINARY_NAMES
#endif

using APKognito.AdbTools;
using APKognito.Configurations;
using APKognito.Exceptions;
using APKognito.Models.Settings;
using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Pages;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace APKognito.Utilities;

public class ApkEditorContext
{
    private const int MAX_SMALI_LOAD_SIZE = 1024 * 20; // 20KB

    private Regex lineReplaceRegex = null!;

    private readonly LoggableObservableObject logger;
    private readonly KognitoConfig kognitoConfig;
    private readonly ApkNameData nameData;

    private readonly ApkRenameSettings renameSettings;

    public string? AssetPath { get; private set; }
    public string OutputApkPath { get; private set; } = string.Empty;

    public ApkEditorContext(
        ApkRenameSettings renameSettings,
        LoggableObservableObject logger,
        bool limited = false
    )
    {
        kognitoConfig = ConfigurationFactory.Instance.GetConfig<KognitoConfig>();

        this.renameSettings = renameSettings;
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
            nameData.OriginalPackageName = GetApkPackageName(Path.Combine(nameData.ApkAssemblyDirectory, "AndroidManifest.xml"));

            // Format new package name and get original company name
            (
                _,
                nameData.OriginalCompanyName,
                nameData.NewPackageName
            ) = SplitPackageName(nameData);
            renameSettings.OnPackageNameFound?.Invoke(nameData.NewPackageName);

            nameData.RenamedApkOutputDirectory = Path.Combine(renameSettings.OutputDirectory, GetFormattedTimeDirectory(nameData.NewPackageName));

            if (!kognitoConfig.ClearTempFilesOnRename)
            {
                Directory.CreateDirectory(nameData.RenamedApkOutputDirectory);

                // Shows where the tmp directory for this package is
                string hiddenFile = Path.Combine(nameData.RenamedApkOutputDirectory, ".tmpdir");
                await File.WriteAllTextAsync(hiddenFile, nameData.ApkAssemblyDirectory, cancellationToken);
                File.SetAttributes(hiddenFile, File.GetAttributes(hiddenFile) | FileAttributes.Hidden);
            }

            // 'nameData' should not be modified after this point.

            // Replace all instances in the APK and any OBBs
            logger.Log($"Changing '{nameData.OriginalPackageName}'  |>  '{nameData.NewPackageName}'");
            lineReplaceRegex = CreateNameReplacementRegex(nameData.OriginalCompanyName);

            await ReplaceAllNameInstancesAsync(cancellationToken);

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
        // Output the unpacked APK in the temp folder
        string args = $"-jar \"{ApkEditorToolPaths.ApktoolJarPath}\" -f d \"{nameData.FullSourceApkPath}\" -o \"{nameData.ApkAssemblyDirectory}\"";
        using Process process = CreateJavaProcess(args);

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

    private async Task<string> PackApkAsync(CancellationToken cToken)
    {
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

    private static async Task AlignPackageAsync(string apkPath)
    {
        string aligned = $"{apkPath}.aligned";

        string args = $"-f -p 4 \"{apkPath}\" \"{aligned}\"";
        await AdbManager.QuickGenericCommand(ApkEditorToolPaths.ZipalignPath, args);

        // c.z.a.apk.aligned -> c.z.a.apk
        File.Delete(apkPath);
        File.Move(aligned, apkPath);
    }

    private async Task SignApkToolAsync(string apkPath, CancellationToken cToken)
    {
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

    private async Task ReplaceAllNameInstancesAsync(CancellationToken cToken)
    {
        logger.LogDebug("Renaming smali directories.");
        ReplaceAllDirectoryNames(nameData.ApkAssemblyDirectory, nameData);

#if CHANGE_BINARY_NAMES
        await ReplaceLibInstancesAsync();
#endif

        string smaliDirectory = Path.Combine(nameData.ApkAssemblyDirectory, "smali");
        IEnumerable<string> files = Directory.EnumerateFiles(smaliDirectory, "*.smali", SearchOption.AllDirectories)
            .Append($"{nameData.ApkAssemblyDirectory}\\AndroidManifest.xml")
            .Append($"{nameData.ApkAssemblyDirectory}\\apktool.yml");

        foreach (string directory in Directory.GetDirectories(nameData.ApkAssemblyDirectory, "smali_*"))
        {
            files = files.Concat(Directory.EnumerateFiles(directory, "*.smali", SearchOption.AllDirectories));
        }

        Directory.CreateDirectory(nameData.ApkSmaliTempDirectory);

#if SINGLE_THREAD_INSTANCE_REPLACING
        string[] filesArr = [.. files];
        logger.LogDebug($"Beginning sequential rename on {filesArr.Length:n0} smali files.");

        foreach (string filePath in filesArr)
        {
            await ReplaceTextInFileAsync(filePath, nameData, cToken);
        }
#else
        logger.LogDebug($"Beginning threaded rename on {files.Count():n0} smali files.");
        await Parallel.ForEachAsync(files, cToken,
            async (filePath, subcToken) =>
            {
                await ReplaceTextInFileAsync(filePath, nameData, subcToken);
            }
        );
#endif

        // This will take the most disk space, so do it only if the actual renaming
        // was successful
        await ReplaceObbFilesAsync();
    }

    private async Task ReplaceObbFilesAsync()
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
        string newObbDirectory = Path.Combine(nameData.RenamedApkOutputDirectory, Path.GetFileName(sourceObbDirectory));
        if (kognitoConfig.CopyFilesWhenRenaming)
        {
            await CopyDirectoryAsync(sourceObbDirectory, newObbDirectory, true);
        }
        else
        {
            _ = Directory.CreateDirectory(nameData.RenamedApkOutputDirectory);
            Directory.Move(sourceObbDirectory, newObbDirectory);
        }

        // Rename the files
        foreach (string filePath in Directory.GetFiles(newObbDirectory, $"*{nameData.FullSourceApkFileName}.obb"))
        {
            string newAssetName = $"{Path.GetFileNameWithoutExtension(filePath).Replace(originalCompanyName, nameData.NewCompanyName)}.obb";

            logger.Log($"Renaming asset file: {Path.GetFileName(filePath)}  |>  {newAssetName}");

            File.Move(filePath, Path.Combine(newObbDirectory, newAssetName));
        }

        string newApkName = nameData.OriginalPackageName.Replace(originalCompanyName, nameData.NewCompanyName);
        RenameDirectory(newObbDirectory, newApkName, nameData);
        AssetPath = Path.Combine(newObbDirectory, newApkName);
    }

    private async Task ReplaceTextInFileAsync(string filePath, ApkNameData nameData, CancellationToken cToken)
    {
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

#if DEBUG
    private async Task ReplaceLibInstancesAsync()
    {
        string libs = Path.Combine(nameData.ApkAssemblyDirectory, "lib");
        if (!Directory.Exists(libs))
        {
            logger.LogDebug("No libs found. Not renaming binaries.");
            return;
        }

        foreach (string file in Directory.EnumerateFiles(libs, "*", SearchOption.AllDirectories))
        {
            logger.Log($"Renaming lib file: {Path.GetFileName(file)}");
            logger.AddIndent();

            await ReplaceBinaryStringsAsync(file,
                Encoding.ASCII.GetBytes(nameData.OriginalCompanyName),
                Encoding.ASCII.GetBytes(nameData.NewCompanyName),
                logger);

            logger.ResetIndent();

            string originalName = Path.GetFileName(file);
            File.Move(file, Path.Combine(Path.GetDirectoryName(file), originalName.Replace(nameData.OriginalCompanyName, nameData.NewCompanyName)));
        }
    }
#endif

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

    private static void ReplaceAllDirectoryNames(string baseDirectory, ApkNameData nameData)
    {
        IEnumerable<string> ienDirs = Directory.GetDirectories(baseDirectory, $"*{nameData.OriginalCompanyName}*", SearchOption.AllDirectories)
            // Organize them to prevent "race conditions", which happens when a parent directory is renamed before a child directory, thereby throwing a DirectoryNotFoundException.
            .OrderByDescending(s => s.Length);

#if DEBUG
        // Not good for memory when dealing with a lot of directories.
        string[] dirs = [.. ienDirs];
        foreach (string directory in dirs)
#else
        foreach (string directory in ienDirs)
#endif
        {
            string adjustedDirectoryName = Path.GetFileName(directory)
                .Replace(nameData.OriginalCompanyName, nameData.NewCompanyName);
            RenameDirectory(directory, adjustedDirectoryName, nameData);
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


        _ = Directory.CreateDirectory(destinationDir);

        _ = Parallel.ForEach(dir.GetFiles(), (file) =>
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            _ = file.CopyTo(targetFilePath, true);
        });

        if (recursive)
        {
            DirectoryInfo[] dirs = dir.GetDirectories();
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                await CopyDirectoryAsync(subDir.FullName, newDestinationDir, true);
            }
        }
    }

    private static string GetApkPackageName(string manifestPath)
    {
        XmlDocument xmlDoc = new();
        xmlDoc.Load(manifestPath);

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
        return $"{sourceApkName} {DateTime.Now:yyyy-MMMM-dd 'at' h.mm}";
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

    private static Regex CreateNameReplacementRegex(string searchValue)
    {
        return new Regex($@"(?<=[\./])({Regex.Escape(searchValue)})(?=[/\.])", RegexOptions.Compiled, TimeSpan.FromMilliseconds(60_000));
    }

#if DEBUG
    private static async Task ReplaceBinaryStringsAsync(string filePath, byte[] searchBytes, byte[] replaceBytes, LoggableObservableObject? logger)
    {
        int searchLength = searchBytes.Length;

        if (searchLength != replaceBytes.Length)
        {
            throw new ArgumentException("Search and replace byte arrays must have the same length.");
        }

        const int BUFFER_SIZE = 1024 * 1024;

        using FileStream binaryStream = new(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, BUFFER_SIZE);

        byte[] buffer = new byte[BUFFER_SIZE];
        long fileLength = binaryStream.Length;
        long filePosition = 0;

        while (filePosition < fileLength)
        {
            int bytesRead = await binaryStream.ReadAsync(buffer.AsMemory(0, BUFFER_SIZE));

            if (bytesRead == 0)
            {
                break;
            }

            int bufferInd = 0;
            while (bufferInd <= bytesRead - searchLength)
            {
                bool match = true;
                for (int sub = 0; sub < searchLength; ++sub)
                {
                    if (buffer[bufferInd + sub] != searchBytes[sub])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    string found = Encoding.ASCII.GetString(buffer[(bufferInd - 40 > 0 ? bufferInd - 40 : bufferInd)..(bufferInd + searchLength + 40)]);
                    found = $"Found: `{found}`, at bInd:{bufferInd}, fPos:{filePosition}, gPos:{filePosition + bufferInd}";
                    Debug.WriteLine(found);
                    logger?.LogDebug(found);

                    binaryStream.Position = filePosition + bufferInd;
                    await binaryStream.WriteAsync(replaceBytes);

                    bufferInd += searchLength; // Move past the replaced section.
                }
                else
                {
                    bufferInd++; // Move to the next byte.
                }
            }

            filePosition += bytesRead;
            if (filePosition < fileLength)
            {
                binaryStream.Position = filePosition;
            }
        }
    }
#endif

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
    }
}

public sealed class ApkRenameSettings
{
    public string SourceApkPath { get; set; } = string.Empty;

    public string OutputDirectory { get; init; } = string.Empty;
    public string JavaPath { get; init; } = string.Empty;
    public string TempDirectory { get; init; } = string.Empty;
    public string ApkReplacementName { get; init; } = string.Empty;

    public Action<string>? OnPackageNameFound { get; init; }
}
