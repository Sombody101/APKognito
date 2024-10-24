﻿using APKognito.Configurations;
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
    private readonly string TempStreamDirectory;

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
        TempStreamDirectory = Path.Combine(viewModel.TempData.FullName, "$streamdata");
        _ = Directory.CreateDirectory(TempStreamDirectory);
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
            HomeViewModel.Log($"Unpacking {FullSourceApkFileName}");
            await UnpackApk(ApkTempDirectory, cancellationToken);

            // Replace package name
            HomeViewModel.Log("Getting package name...");
            string androidManifest = Path.Combine(ApkTempDirectory, "AndroidManifest.xml");
            string packageName = GetApkPackageName(androidManifest);

            (string oldCompanyName, string newPackageName) = SplitPackageName(packageName);
            viewModel.FinalName = newPackageName;

            string finalOutputDirectory = Path.Combine(OutputDirectory, $"{newPackageName}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");

            HomeViewModel.Log($"Changing '{packageName}'  →  '{newPackageName}'");

            // await Task.WhenAll(
            // ReplaceTextInFileAsync(androidManifest, oldCompanyName, ReplacementCompanyName, cancellationToken),
            // ReplaceTextInFileAsync(Path.Combine(ApkTempDirectory, "apktool.yml"), oldCompanyName, ReplacementCompanyName, cancellationToken),
            await ReplaceAllNameInstancesAsync(oldCompanyName, ReplacementCompanyName, cancellationToken);
            //)

            ReplaceObbFiles(packageName, ReplacementCompanyName, finalOutputDirectory);

            // Repack
            HomeViewModel.Log("Packing APK...");
            string unsignedApk = await PackApk(newPackageName, cancellationToken);

            // Sign
            HomeViewModel.Log("Singing APK...");
            await SignApkTool(unsignedApk, finalOutputDirectory, cancellationToken);

            // Copy to output and cleanup
            HomeViewModel.Log($"Finished APK {newPackageName}.");

            HomeViewModel.Log("Cleaning up...");
            Directory.Delete(ApkTempDirectory, true);

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
                    HomeViewModel.LogWarning($"Failed to clear source APK (CopyWhenRenaming=Enabled): {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            return "Job canceled.";
        }
        catch (Exception ex)
        {
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
                // No need to move the source, if it's already unpacked
                File.Delete(FullSourceApkPath);
            }

            return;
        }

        string error = await process.StandardError.ReadToEndAsync(cToken);
        HomeViewModel.LogError($"Failed to unpack {FullSourceApkFileName}. Error: {error}");
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

    private async Task SignApkTool(string apkPath, string outputApkPath, CancellationToken cToken)
    {
        string args = $"-jar \"{viewModel.ApksignerJar}\" -a \"{apkPath}\" -o \"{outputApkPath}\" --allowResign";
        using Process process = CreateJavaProcess(args);

        _ = process.Start();
        await process.WaitForExitAsync(cToken);

        string error = await process.StandardError.ReadToEndAsync(cToken);

        if (process.ExitCode is not 0)
        {
            throw new RenameFailedException(error);
        }

        // Rename the output APK
        string trueName = Path.Combine(outputApkPath, Path.GetFileName(apkPath).Replace(".unsigned.apk", string.Empty));
        string newSignedName = $"{trueName}.unsigned-aligned-debugSigned";

        File.Move($"{newSignedName}.apk", $"{trueName}.apk", true);
        File.Move($"{newSignedName}.apk.idsig", $"{trueName}.apk.idsig", true);
    }

    private async Task ReplaceAllNameInstancesAsync(string searchCompanyName, string replacementName, CancellationToken cToken)
    {
        RenameDirectory(Path.Combine(ApkTempDirectory, "smali\\com", searchCompanyName), replacementName);

        string[] files = Directory.GetFiles(Path.Combine(ApkTempDirectory, "smali"), "*", SearchOption.AllDirectories);

        await Parallel.ForEachAsync(files, cToken, async (filePath, subcToken)
            => await ReplaceTextInFileAsync(filePath, searchCompanyName, replacementName, subcToken)
        );
    }

    private void ReplaceObbFiles(string sourcePackageName, string newApkCompany, string outputDirectory)
    {
        string originalCompanyName = sourcePackageName.Split('.')[1];
        string? sourceDirectory = Path.GetDirectoryName(FullSourceApkPath);

        if (sourceDirectory is null)
        {
            HomeViewModel.LogError("Failed to get APK source directory. No OBB files will be renamed even if they're present.");
            return;
        }

        // Rename OBB file (if there is one)
        string obbDirectory = Path.Combine(sourceDirectory, sourcePackageName);

        if (!Directory.Exists(obbDirectory))
        {
            return;
        }

        string newObbDirectory = Path.Combine(outputDirectory, Path.GetFileName(obbDirectory));
        if (config.CopyFilesWhenRenaming)
        {
            CopyDirectory(obbDirectory, newObbDirectory, true);
        }
        else
        {
            _ = Directory.CreateDirectory(outputDirectory);
            Directory.Move(obbDirectory, newObbDirectory);
        }

        string finalPath = Path.Combine(outputDirectory, sourcePackageName);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(FullSourceApkFileName);

        foreach (string filePath in Directory.GetFiles(newObbDirectory, $"*{fileNameWithoutExtension}.obb"))
        {
            HomeViewModel.Log($"Renaming app file {Path.GetFileName(filePath)}");

            string newFileName = fileNameWithoutExtension.Replace(originalCompanyName, newApkCompany);
            string newFilePath = Path.Combine(finalPath, $"{newFileName}{Path.GetExtension(filePath)}");

            File.Move(filePath, newFilePath, true);
        }

        string newApkName = sourcePackageName.Replace(originalCompanyName, newApkCompany);
        RenameDirectory(newObbDirectory, newApkName);
    }

    private static async Task ReplaceTextInFileAsync(string filePath, string searchText, string replaceText, CancellationToken cToken)
    {
        using StreamReader reader = File.OpenText(filePath);
        await using MemoryStream memoryStream = new();
        await using StreamWriter writer = new(memoryStream);

        string? line;
        while ((line = await reader.ReadLineAsync(cToken)) is not null && !cToken.IsCancellationRequested)
        {
            await writer.WriteLineAsync(line.Replace(searchText, replaceText).AsMemory(), cToken);
        }

        memoryStream.SetLength(memoryStream.Position);
        _ = memoryStream.Seek(0, SeekOrigin.Begin);

        reader.Close();

        // Flushing the stream here creates redundant data...

        await using FileStream copyStream = File.OpenWrite(filePath);
        await memoryStream.CopyToAsync(copyStream, cToken);
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

    private static void RenameDirectory(string directory, string newName)
    {
        string newFolderPath = Path.Combine(Path.GetDirectoryName(directory)!, newName);

        Directory.Move(directory, newFolderPath);
    }

    private static void CopyDirectory(string sourceDir, string destinationDir, bool recursive = false)
    {
        DirectoryInfo dir = new(sourceDir);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
        }

        DirectoryInfo[] dirs = dir.GetDirectories();

        _ = Directory.CreateDirectory(destinationDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            _ = file.CopyTo(targetFilePath, true);
        }

        if (recursive)
        {
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir, true);
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

    private (string, string) SplitPackageName(string packageName)
    {
        // Get original package name and create output package name
        string[] split = packageName.Split('.');
        string oldCompanyName = split[1];
        split[1] = ReplacementCompanyName;

        string newPackageName = string.Join('.', split);

        return (oldCompanyName, newPackageName);
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