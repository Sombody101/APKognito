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

    /*
     * Private fields
     */

    private readonly string JavaPath;
    private readonly string FullSourceApkPath;
    private readonly string FullSourceApkFileName;
    private readonly string ReplacementCompanyName;
    private readonly string OutputDirectory;

    private readonly string ApkTempDirectory;

    public string OutputApkPath { get; private set; } = string.Empty;
    public string? AssetPath { get; private set; }

    public ApkEditorContext(
        HomeViewModel homeViewModel,
        string javaPath,
        string sourceApkPath
        )
    {
        config = ConfigurationFactory.GetConfig<KognitoConfig>();
        viewModel = homeViewModel;

        JavaPath = javaPath;
        FullSourceApkPath = sourceApkPath;
        FullSourceApkFileName = Path.GetFileName(sourceApkPath);
        ReplacementCompanyName = viewModel.ApkReplacementName;
        OutputDirectory = viewModel.OutputDirectory;

        // Temporary directories

        ApkTempDirectory = Path.Combine(viewModel.TempData.FullName, $"{FullSourceApkFileName[..^4]}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");
        _ = Directory.CreateDirectory(ApkTempDirectory);
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
            viewModel.Log($"Unpacking {FullSourceApkFileName}");
            await UnpackApk(ApkTempDirectory, cancellationToken);

            // Get original package name
            viewModel.Log("Getting package name...");
            string androidManifest = Path.Combine(ApkTempDirectory, "AndroidManifest.xml");
            string packageName = GetApkPackageName(androidManifest);

            // format new package name and get original company name
            (
                string packagePrefix,
                string oldCompanyName, 
                string newPackageName
            ) = SplitPackageName(packageName);
            viewModel.FinalName = newPackageName;
            string finalOutputDirectory = Path.Combine(OutputDirectory, $"{newPackageName}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");

            // Replace all instances in the APK and any OBBs
            viewModel.Log($"Changing '{packageName}'  |>  '{newPackageName}'");
            await ReplaceAllNameInstancesAsync(packagePrefix, oldCompanyName, ReplacementCompanyName, cancellationToken);
            await ReplaceObbFiles(packageName, ReplacementCompanyName, finalOutputDirectory);

            // Repack
            viewModel.Log("Packing APK...");
            string unsignedApk = await PackApk(newPackageName, cancellationToken);

            // Sign
            viewModel.Log("Singing APK...");
            await SignApkTool(unsignedApk, finalOutputDirectory, cancellationToken);

            // Copy to output and cleanup
            viewModel.Log($"Finished APK {newPackageName}");
            viewModel.Log($"Placed into: {finalOutputDirectory}");
            viewModel.Log("Cleaning up...");

            if (config.ClearTempFilesOnRename)
            {
                Directory.Delete(ApkTempDirectory, true);
            }

            if (!config.CopyFilesWhenRenaming)
            {
                try
                {
                    File.Delete(FullSourceApkPath);

                    string obbDirectory = Path.GetDirectoryName(FullSourceApkPath)
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
            return $"{(ex.InnerException ?? ex).GetType().Name}: {ex.Message}\n{ex.StackTrace}";
#else
            return $"{(ex.InnerException ?? ex).GetType().Name}: {ex.Message}";
#endif
        }

        return null;
    }

    private async Task UnpackApk(string outputDirectory, CancellationToken cToken)
    {
        string args = $"-jar \"{viewModel.ApktoolJar}\" -f d \"{FullSourceApkPath}\" -o \"{outputDirectory}\"";
        using Process process = CreateJavaProcess(args);

        _ = process.Start();
        await process.WaitForExitAsync(cToken);

        if (process.ExitCode is 0)
        {
            if (!config.CopyFilesWhenRenaming)
            {
                File.Delete(FullSourceApkPath);
            }

            return;
        }

        string error = await process.StandardError.ReadToEndAsync(cToken);
        viewModel.LogError($"Failed to unpack {FullSourceApkFileName}. Error: {error}");
        throw new RenameFailedException(error);
    }

    private async Task<string> PackApk(string newPackageName, CancellationToken cToken)
    {
        string apkName = $"{newPackageName}.unsigned.apk";
        string outputApkName = Path.Combine(ApkTempDirectory, apkName);

        string args = $"-jar \"{viewModel.ApktoolJar}\" -f b \"{ApkTempDirectory}\" -o \"{outputApkName}\"";
        using Process process = CreateJavaProcess(args);

        _ = process.Start();
        await process.WaitForExitAsync(cToken);

        return process.ExitCode is not 0
            ? throw new RenameFailedException(await process.StandardError.ReadToEndAsync(cToken))
            : outputApkName;
    }

    private async Task SignApkTool(string apkPath, string outputApkDirectory, CancellationToken cToken)
    {
        string args = $"-jar \"{viewModel.ApksignerJar}\" -a \"{apkPath}\" -o \"{outputApkDirectory}\" --allowResign";
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
        string fullTrueName = Path.Combine(outputApkDirectory, Path.GetFileName(apkPath).Replace(".unsigned.apk", string.Empty));
        string newSignedName = $"{fullTrueName}.unsigned-aligned-debugSigned";

        // Paths for pushing to a device
        AssetPath = fullTrueName;
        OutputApkPath = $"{fullTrueName}.apk";

        File.Move($"{newSignedName}.apk", OutputApkPath, true);
        File.Move($"{newSignedName}.apk.idsig", $"{fullTrueName}.apk.idsig", true);
    }

    private async Task ReplaceAllNameInstancesAsync(string packagePrefix, string searchCompanyName, string replacementName, CancellationToken cToken)
    {
        ReplaceAllDirectoryNames(ApkTempDirectory, searchCompanyName, replacementName);

        string[] files = Directory.GetFiles(Path.Combine(ApkTempDirectory, "smali", packagePrefix, replacementName), "*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".smali"))
            .Append($"{ApkTempDirectory}\\AndroidManifest.xml")
            .Append($"{ApkTempDirectory}\\apktool.yml").ToArray();

        await Parallel.ForEachAsync(files, cToken,
            async (filePath, subcToken) =>
            await ReplaceTextInFileAsync(filePath, searchCompanyName, replacementName, subcToken)
        );
    }

    private async Task ReplaceObbFiles(string sourcePackageName, string newApkCompany, string outputDirectory)
    {
        string originalCompanyName = sourcePackageName.Split('.')[1];
        string? sourceDirectory = Path.GetDirectoryName(FullSourceApkPath);

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

        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(FullSourceApkFileName);

        foreach (string filePath in Directory.GetFiles(newObbDirectory, $"*{fileNameWithoutExtension}.obb"))
        {
            string newAssetName = $"{Path.GetFileNameWithoutExtension(filePath).Replace(originalCompanyName, newApkCompany)}.obb";

            viewModel.Log($"Renaming asset file: {Path.GetFileName(filePath)}  |>  {newAssetName}");

            File.Move(filePath, Path.Combine(newObbDirectory, newAssetName));
        }

        string newApkName = sourcePackageName.Replace(originalCompanyName, newApkCompany);
        RenameDirectory(newObbDirectory, newApkName);
        AssetPath = Path.Combine(newObbDirectory, newApkName);
    }

    private static async Task ReplaceTextInFileAsync(string filePath, string searchText, string replaceText, CancellationToken cToken)
    {
        // These files usually aren't that big, so just load all of it into memory and replace it.
        await File.WriteAllTextAsync(
            filePath,
            (await File.ReadAllTextAsync(filePath, cToken)).Replace(searchText, replaceText),
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

    private void RenameDirectory(string directory, string newName)
    {
        string newFolderPath = Path.Combine(Path.GetDirectoryName(directory)!, newName);

        if (Directory.Exists(newFolderPath))
        {
            viewModel.LogWarning($"Directory '{newName}' already exists, deleting and replacing. This may cause your APK to not load, " +
                "if so, pick a new company name so it's not conflicting.");
        }

        Directory.Move(directory, newFolderPath);
    }

    private void ReplaceAllDirectoryNames(string baseDirectory, string searchName, string replacementName)
    {
        Parallel.ForEach(
            Directory.GetDirectories(baseDirectory, $"*{searchName}*", SearchOption.AllDirectories),
            (directory) => RenameDirectory(directory, replacementName)
        );
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

        await Parallel.ForEachAsync(dir.GetFiles(), (file, token) =>
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            _ = file.CopyTo(targetFilePath, true);
            return ValueTask.CompletedTask;
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
            ?? throw new RenameFailedException("Failed to get package name.");
    }

    private (string, string, string) SplitPackageName(string packageName)
    {
        string[] split = packageName.Split('.');

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
        return (split[0], oldCompanyName, packageName.Replace(oldCompanyName, ReplacementCompanyName));
    }

    public static long CalculateApkSize(string apkPath, bool copyingFile = true)
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
}