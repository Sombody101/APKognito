using APKognito.Configurations;
using APKognito.Exceptions;
using APKognito.Models.Settings;
using APKognito.ViewModels.Pages;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Xml;

namespace APKognito.Utilities;

public class ApkEditorContext
{
    private readonly HomeViewModel viewModel;
    private readonly KognitoConfig config;
    private readonly ApkNameData nameData;

    /*
     * Private fields
     */

    private readonly string JavaPath;

    public string? AssetPath { get; private set; }
    public string OutputApkPath { get; private set; } = string.Empty;

    public ApkEditorContext(
        HomeViewModel homeViewModel,
        string javaPath,
        string sourceApkPath
        )
    {
        config = ConfigurationFactory.GetConfig<KognitoConfig>();

        JavaPath = javaPath;
        viewModel = homeViewModel;

        nameData = new()
        {
            FullSourceApkPath = sourceApkPath,
            FullSourceApkFileName = Path.GetFileName(sourceApkPath),
            NewCompanyName = homeViewModel.ApkReplacementName,
        };
        nameData.ApkAssemblyDirectory = Path.Combine(viewModel.TempData.FullName, $"{nameData.FullSourceApkFileName[..^4]}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");

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
            viewModel.FinalName = "Unpacking...";

            // Unpack
            viewModel.Log($"Unpacking {nameData.FullSourceApkFileName}");
            await UnpackApk(cancellationToken);

            // Get original package name
            viewModel.Log("Getting package name...");
            nameData.OriginalPackageName = GetApkPackageName(Path.Combine(nameData.ApkAssemblyDirectory, "AndroidManifest.xml"));

            // Format new package name and get original company name
            (
                nameData.OriginalCompanyName,
                nameData.NewPackageName
            ) = SplitPackageName(nameData);
            viewModel.FinalName = nameData.NewPackageName;
            nameData.RenamedApkOutputDirectory = Path.Combine(viewModel.OutputDirectory, $"{nameData.NewPackageName}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");

            // 'nameData' should not be modified after this point.

            // Replace all instances in the APK and any OBBs
            viewModel.Log($"Changing '{nameData.OriginalPackageName}'  |>  '{nameData.NewPackageName}'");
            await ReplaceAllNameInstancesAsync(nameData, cancellationToken);
            await ReplaceObbFiles(nameData.OriginalPackageName, nameData.NewCompanyName, nameData.RenamedApkOutputDirectory);

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
                Directory.Delete(nameData.ApkAssemblyDirectory, true);
            }

            if (!config.CopyFilesWhenRenaming)
            {
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

    private async Task UnpackApk(CancellationToken cToken)
    {
        // Output the unpacked APK in the temp folder
        string args = $"-jar \"{viewModel.ApktoolJar}\" -f d \"{nameData.FullSourceApkPath}\" -o \"{nameData.ApkAssemblyDirectory}\"";
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

        string args = $"-jar \"{viewModel.ApktoolJar}\" -f b \"{nameData.ApkAssemblyDirectory}\" -o \"{outputApkPath}\"";
        using Process process = CreateJavaProcess(args);

        _ = process.Start();
        await process.WaitForExitAsync(cToken);

        return process.ExitCode is not 0
            ? throw new RenameFailedException(await process.StandardError.ReadToEndAsync(cToken))
            : outputApkPath;
    }

    private async Task SignApkTool(string apkPath, CancellationToken cToken)
    {
        string args = $"-jar \"{viewModel.ApksignerJar}\" -a \"{apkPath}\" -o \"{nameData.RenamedApkOutputDirectory}\" --allowResign";
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

    private static async Task ReplaceAllNameInstancesAsync(ApkNameData nameData, CancellationToken cToken)
    {
        ReplaceAllDirectoryNames(nameData.ApkAssemblyDirectory, nameData);

        IEnumerable<string> files = Directory.EnumerateFiles(nameData.ApkAssemblyDirectory, "*.smali", SearchOption.AllDirectories)
            .Append($"{nameData.ApkAssemblyDirectory}\\AndroidManifest.xml")
            .Append($"{nameData.ApkAssemblyDirectory}\\apktool.yml");

        await Parallel.ForEachAsync(files, cToken,
            async (filePath, subcToken) =>
            {
                await ReplaceTextInFileAsync(filePath, nameData, subcToken);
            }
        );
    }

    private async Task ReplaceObbFiles(string sourcePackageName, string newApkCompany, string outputDirectory)
    {
        string originalCompanyName = sourcePackageName.Split('.')[1];
        string? sourceDirectory = Path.GetDirectoryName(nameData.FullSourceApkPath);

        if (sourceDirectory is null)
        {
            viewModel.LogError("Failed to get APK source directory. No OBB files will be renamed even if they're present.");
            return;
        }

        string obbDirectory = Path.Combine(sourceDirectory, sourcePackageName);
        if (!Directory.Exists(obbDirectory))
        {
            return;
        }

        string newObbDirectory = Path.Combine(outputDirectory, Path.GetFileName(obbDirectory));
        if (config.CopyFilesWhenRenaming)
        {
            await CopyDirectory(obbDirectory, newObbDirectory, true);
        }
        else
        {
            _ = Directory.CreateDirectory(outputDirectory);
            Directory.Move(obbDirectory, newObbDirectory);
        }

        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(nameData.FullSourceApkFileName);

        foreach (string filePath in Directory.GetFiles(newObbDirectory, $"*{fileNameWithoutExtension}.obb"))
        {
            string newAssetName = $"{Path.GetFileNameWithoutExtension(filePath).Replace(originalCompanyName, newApkCompany)}.obb";

            viewModel.Log($"Renaming asset file: {Path.GetFileName(filePath)}  |>  {newAssetName}");

            File.Move(filePath, Path.Combine(newObbDirectory, newAssetName));
        }

        string newApkName = sourcePackageName.Replace(originalCompanyName, newApkCompany);
        RenameDirectory(newObbDirectory, newApkName, nameData);
        AssetPath = Path.Combine(newObbDirectory, newApkName);
    }

    private static async Task ReplaceTextInFileAsync(string filePath, ApkNameData nameData, CancellationToken cToken)
    {
        // These files usually aren't that big, so just load all of it into memory and replace it.
        await File.WriteAllTextAsync(
            filePath,
            (await File.ReadAllTextAsync(filePath, cToken)).Replace(nameData.OriginalCompanyName, nameData.NewCompanyName),
            cToken
        );
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
            FileLogger.LogWarning($"The directory '.{trimmedDirectory}' has already been renamed to {newName}, skipping.");
            return;
        }

        Directory.Move(originalDirectory, newFolderPath);
    }

    private static void ReplaceAllDirectoryNames(string baseDirectory, ApkNameData nameData)
    {
        string[] dirs = [.. Directory.GetDirectories(baseDirectory, $"*{nameData.OriginalCompanyName}*", SearchOption.AllDirectories)
            .OrderByDescending(s => s.Length)];

        foreach (string directory in dirs)
        {
            string adjustedDirectoryName = Path.GetFileName(directory).Replace(nameData.OriginalCompanyName, nameData.NewCompanyName);
            RenameDirectory(directory, adjustedDirectoryName, nameData);
        }
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

        Parallel.ForEach(dir.GetFiles(), (file) =>
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

    private static (string, string) SplitPackageName(ApkNameData nameData)
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
        return (oldCompanyName, nameData.OriginalPackageName.Replace(oldCompanyName, nameData.NewCompanyName));
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

    /// <summary>
    /// This is to give the illusion of organization.
    /// </summary>
    private sealed class ApkNameData
    {
        public string OriginalPackageName { get; set; } = string.Empty;
        public string OriginalCompanyName { get; set; } = string.Empty;

        public string NewPackageName { get; set; } = string.Empty;
        public string NewCompanyName { get; set; } = string.Empty;

        public string FullSourceApkPath { get; set; } = string.Empty;
        public string FullSourceApkFileName { get; set; } = string.Empty;

        public string RenamedApkOutputDirectory { get; set; } = string.Empty;
        public string ApkAssemblyDirectory { get; set; } = string.Empty;
    }
}
