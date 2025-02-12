#if DEBUG
// Only used for debugging (multiple threads running will disrupt stepthrough-debugging by triggering breakpoints)
//#define SINGLE_THREAD_INSTANCE_REPLACING
#endif

using APKognito.Configurations;
using APKognito.Exceptions;
using APKognito.Models.Settings;
using APKognito.ViewModels.Pages;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;

namespace APKognito.Utilities;

public class ApkEditorContext
{
    private const int MAX_SMALI_LOAD_SIZE = 1024 * 20; // 20KB

    private Regex lineReplaceRegex;

    private readonly HomeViewModel viewModel;
    private readonly KognitoConfig config;
    private readonly ApkNameData nameData;

    private readonly string JavaPath;

    public string? AssetPath { get; private set; }
    public string OutputApkPath { get; private set; } = string.Empty;

    public ApkEditorContext(
        HomeViewModel homeViewModel,
        string javaPath,
        string sourceApkPath,
        bool limited = false
    )
    {
        config = ConfigurationFactory.GetConfig<KognitoConfig>();

        JavaPath = javaPath;
        viewModel = homeViewModel;

        string sourceApkName = Path.GetFileName(sourceApkPath);
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
            FullSourceApkPath = sourceApkPath,
            FullSourceApkFileName = sourceApkName,
            NewCompanyName = homeViewModel.ApkReplacementName,
            ApkSmaliTempDirectory = Path.Combine(viewModel.TempData.FullName, "$smali"),
            ApkAssemblyDirectory = Path.Combine(viewModel.TempData.FullName, GetFormattedTimeDirectory(sourceApkName))
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
    public async Task<string?> RenameApk(CancellationToken cancellationToken)
    {
        // This method is a big mess due to the usage of Path.Combine(). I've tried to clean it up as
        // much as I can without defining so many strings.

        try
        {
            // Unpack
            viewModel.FinalName = "Unpacking...";
            viewModel.Log($"Unpacking {nameData.FullSourceApkFileName}");
            await UnpackApk(cancellationToken);

            // Get original package name
            viewModel.Log("Getting package name...");
            nameData.OriginalPackageName = GetApkPackageName(Path.Combine(nameData.ApkAssemblyDirectory, "AndroidManifest.xml"));

            // Format new package name and get original company name
            (
                string packagePrefix,
                nameData.OriginalCompanyName,
                nameData.NewPackageName
            ) = SplitPackageName(nameData);
            viewModel.FinalName = nameData.NewPackageName;
            nameData.RenamedApkOutputDirectory = Path.Combine(viewModel.OutputDirectory, GetFormattedTimeDirectory(nameData.NewPackageName));


            // 'nameData' should not be modified after this point.

            // Replace all instances in the APK and any OBBs
            viewModel.Log($"Changing '{nameData.OriginalPackageName}'  |>  '{nameData.NewPackageName}'");
            lineReplaceRegex = CreateNameReplacementRegex(nameData.OriginalCompanyName);

            await ReplaceAllNameInstancesAsync(packagePrefix, cancellationToken);
            await ReplaceObbFiles();

            // Repack
            viewModel.Log("Packing APK...");
            string unsignedApk = await PackApk(cancellationToken);

            // Sign
            viewModel.Log("Signing APK...");
            await SignApkTool(unsignedApk, cancellationToken);

            // Copy to output and cleanup
            viewModel.Log($"Finished APK {nameData.NewPackageName}");
            viewModel.Log($"Placed into: {nameData.RenamedApkOutputDirectory}");
            viewModel.Log("Cleaning up...");

            if (config.ClearTempFilesOnRename)
            {
                viewModel.LogDebug($"Clearing temp directory `{nameData.ApkAssemblyDirectory}`");
                Directory.Delete(nameData.ApkAssemblyDirectory, true);
            }

            if (!config.CopyFilesWhenRenaming)
            {
                viewModel.LogDebug($"CopyWhenRenaming enabled, deleting directory `{nameData.FullSourceApkPath}`");

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
                    viewModel.LogWarning($"Failed to clear source APK (CopyWhenRenaming=Enabled): {ex.Message}");
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
            // Exceptions are added to the exceptions log file, so there's no reason to add a stack trace here.
            // Will that stop people from copying the entire LogBox control and pasting it in their GitHub issue along with the logpack which already contains that information?
            // Nope.
            return $"{(ex.InnerException ?? ex).GetType().Name}: {ex.Message}\n{ex.StackTrace}";
#else
            return $"{(ex.InnerException ?? ex).GetType().Name}: {ex.Message}";
#endif
        }

        return null;
    }

    public async Task UnpackApk(string apkPath, string outputDirectory, CancellationToken cToken = default)
    {
        string args = $"-jar \"{viewModel.ApktoolJarPath}\" -f d \"{apkPath}\" -o \"{outputDirectory}\"";
        using Process process = CreateJavaProcess(args);

        _ = process.Start();
        await process.WaitForExitAsync(cToken);

        if (process.ExitCode is 0)
        {
            return;
        }

        string error = await process.StandardError.ReadToEndAsync(cToken);
        viewModel.LogError($"Failed to unpack {apkPath}. Error: {error}");
        throw new RenameFailedException(error);
    }

    private async Task UnpackApk(CancellationToken cToken)
    {
        // Output the unpacked APK in the temp folder
        string args = $"-jar \"{viewModel.ApktoolJarPath}\" -f d \"{nameData.FullSourceApkPath}\" -o \"{nameData.ApkAssemblyDirectory}\"";
        using Process process = CreateJavaProcess(args);

        _ = process.Start();
        await process.WaitForExitAsync(cToken);

        if (process.ExitCode is 0)
        {
            if (!config.CopyFilesWhenRenaming)
            {
                File.Delete(nameData.FullSourceApkPath);
            }

            return;
        }

        string error = await process.StandardError.ReadToEndAsync(cToken);
        viewModel.LogError($"Failed to unpack {nameData.FullSourceApkFileName}. Error: {error}");
        throw new RenameFailedException(error);
    }

    private async Task<string> PackApk(CancellationToken cToken)
    {
        string outputApkPath = Path.Combine(
            nameData.ApkAssemblyDirectory,
            $"{nameData.NewPackageName}.unsigned.apk"
        );

        string args = $"-jar \"{viewModel.ApktoolJarPath}\" -f b \"{nameData.ApkAssemblyDirectory}\" -o \"{outputApkPath}\"";
        using Process process = CreateJavaProcess(args);

        _ = process.Start();
        await process.WaitForExitAsync(cToken);

        return process.ExitCode is not 0
            ? throw new RenameFailedException(await process.StandardError.ReadToEndAsync(cToken))
            : outputApkPath;
    }

    private async Task SignApkTool(string apkPath, CancellationToken cToken)
    {
        string args = $"-jar \"{viewModel.ApksignerJarPath}\" -a \"{apkPath}\" -o \"{nameData.RenamedApkOutputDirectory}\" --allowResign";
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

    private async Task ReplaceAllNameInstancesAsync(string packagePrefix, CancellationToken cToken)
    {
        viewModel.LogDebug("Renaming smali directories.");
        ReplaceAllDirectoryNames(nameData.ApkAssemblyDirectory, nameData);

        IEnumerable<string> files = Directory.EnumerateFiles(Path.Combine(nameData.ApkAssemblyDirectory, "smali"), "*.smali", SearchOption.AllDirectories)
            .Append($"{nameData.ApkAssemblyDirectory}\\AndroidManifest.xml")
            .Append($"{nameData.ApkAssemblyDirectory}\\apktool.yml");

        foreach (string directory in Directory.GetDirectories(nameData.ApkAssemblyDirectory, "smali_*"))
        {
            files = files.Concat(Directory.EnumerateFiles(directory, "*.smali", SearchOption.AllDirectories));
        }

        Directory.CreateDirectory(nameData.ApkSmaliTempDirectory);

#if SINGLE_THREAD_INSTANCE_REPLACING
        string[] filesArr = [.. files];
        viewModel.LogDebug($"Beginning rename on {filesArr.Length:n0} smali files.");

        foreach (string filePath in filesArr)
        {
            await ReplaceTextInFileAsync(filePath, nameData, cToken);
        }
#else
        viewModel.LogDebug($"Beginning rename on {files.Count():n0} smali files.");
        await Parallel.ForEachAsync(files, cToken,
            async (filePath, subcToken) =>
            {
                await ReplaceTextInFileAsync(filePath, nameData, subcToken);
            }
        );
#endif
    }

    private async Task ReplaceObbFiles()
    {
        string originalCompanyName = nameData.OriginalCompanyName;
        string? sourceDirectory = Path.GetDirectoryName(nameData.FullSourceApkPath);

        if (sourceDirectory is null)
        {
            viewModel.LogError("Failed to get APK source directory. No OBB files will be renamed even if they're present.");
            return;
        }

        string obbDirectory = Path.Combine(sourceDirectory, nameData.OriginalPackageName);
        if (!Directory.Exists(obbDirectory))
        {
            return;
        }

        string newObbDirectory = Path.Combine(nameData.RenamedApkOutputDirectory, Path.GetFileName(obbDirectory));
        if (config.CopyFilesWhenRenaming)
        {
            await CopyDirectory(obbDirectory, newObbDirectory, true);
        }
        else
        {
            _ = Directory.CreateDirectory(nameData.RenamedApkOutputDirectory);
            Directory.Move(obbDirectory, newObbDirectory);
        }

        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(nameData.FullSourceApkFileName);

        foreach (string filePath in Directory.GetFiles(newObbDirectory, $"*{fileNameWithoutExtension}.obb"))
        {
            string newAssetName = $"{Path.GetFileNameWithoutExtension(filePath).Replace(originalCompanyName, nameData.NewCompanyName)}.obb";

            viewModel.Log($"Renaming asset file: {Path.GetFileName(filePath)}  |>  {newAssetName}");

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

    private Process CreateJavaProcess(string arguments)
    {
        return new()
        {
            StartInfo = new()
            {
                FileName = JavaPath,
                Arguments = arguments,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
    }

    private static void ReplaceAllDirectoryNames(string baseDirectory, ApkNameData nameData)
    {
        IEnumerable<string> ienDirs = Directory.GetDirectories(baseDirectory, nameData.OriginalCompanyName, SearchOption.AllDirectories)
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

        string trimmedDirectory = originalDirectory[nameData.ApkAssemblyDirectory.Length..];
        FileLogger.Log($"Changing .{trimmedDirectory} -> {newName}");

        string newFolderPath = Path.Combine(Path.GetDirectoryName(originalDirectory)!, newName);

        if (Directory.Exists(newFolderPath))
        {
            FileLogger.LogWarning($"The directory '{trimmedDirectory}' has already been renamed to {newName}, skipping.");
            return;
        }

        Directory.Move(originalDirectory, newFolderPath);
    }

    private static async Task CopyDirectory(string sourceDir, string destinationDir, bool recursive = false)
    {
        DirectoryInfo dir = new(sourceDir);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
        }

        DirectoryInfo[] dirs = dir.GetDirectories();

        _ = Directory.CreateDirectory(destinationDir);

        _ = Parallel.ForEach(dir.GetFiles(), (file) =>
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            _ = file.CopyTo(targetFilePath, true);
        });

        if (recursive)
        {
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                await CopyDirectory(subDir.FullName, newDestinationDir, true);
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
