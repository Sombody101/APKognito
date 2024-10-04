using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Documents;
using System.Windows.Media;
using System.Xml;
using Wpf.Ui.Controls;

namespace APKognito.ViewModels.Pages;

public partial class HomeViewModel
{
    // This is NOT the correct way to do this, but I don't want to setup
    // a convoluted converter for any of this
    public void AntiMvvm_ConfigureLogger(RichTextBox _logBox)
    {
        logBox = _logBox;
        logBox.Document.FontFamily = firaRegular;
    }

    public void WriteGenericLog(string text, [Optional] Brush color)
    {
        logBox.Dispatcher.Invoke(() =>
        {
            Run log = new(text)
            {
                FontFamily = firaRegular
            };

            if (color is not null)
            {
                log.Foreground = color;
            }

    ((Paragraph)logBox.Document.Blocks.LastBlock).Inlines.Add(log);
            logBox.ScrollToEnd();
        });
    }

    public void Log(string log)
    {
        WriteGenericLog($"[INFO]    ~ {log}\n");
    }

    public void LogWarning(string log)
    {
        WriteGenericLog($"[WARNING] # {log}\n", Brushes.Yellow);
    }

    public void LogError(string log)
    {
        WriteGenericLog($"[ERROR]   ! {log}\n", Brushes.Red);
    }

    public void ClearLogs()
    {
        ((Paragraph)logBox.Document.Blocks.LastBlock).Inlines.Clear();
    }

    public string[]? GetFilePaths()
    {
        return config?.ApkSourcePath?.Split(PathSeparator);
    }

    private async Task UnpackApk(string javaPath, string sourceApk, string outputDirectory)
    {
        using Process process = CreateJavaProcess(javaPath, $"-jar \"{apktoolJar}\" -f d \"{sourceApk}\" -o \"{outputDirectory}\"");

        _ = process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode is 0)
        {
            if (config.CopyFilesWhenRenaming)
            {
                // No need to move the source, if it's already unpacked 
                File.Delete(sourceApk);
            }

            return;
        }

        string error = await process.StandardError.ReadToEndAsync();
        LogError($"Failed to unpack {sourceApk}. Error: {error}");
        throw new Exception(error);
    }

    private async Task<string> PackApk(string javaPath, string directoryPath, string newPackageName)
    {
        string apkName = $"{newPackageName}.unsigned.apk";
        string outputApkName = Path.Combine(directoryPath, apkName);

        using Process process = CreateJavaProcess(javaPath, $"-jar \"{apktoolJar}\" -f b \"{directoryPath}\" -o \"{outputApkName}\"");

        _ = process.Start();
        await process.WaitForExitAsync();

        return process.ExitCode is not 0 ? throw new Exception(await process.StandardError.ReadToEndAsync()) : outputApkName;
    }

    private async Task SignApkTool(string javaPath, string apkPath, string outputApkPath)
    {
        using Process process = CreateJavaProcess(javaPath, $"-jar \"{apksignerJar}\" -a \"{apkPath}\" -o \"{outputApkPath}\" --allowResign");

        _ = process.Start();
        await process.WaitForExitAsync();

        string error = await process.StandardError.ReadToEndAsync();

        if (process.ExitCode is not 0)
        {
            throw new Exception(error);
        }

        // Rename the output APK
        string trueName = Path.Combine(outputApkPath, Path.GetFileName(apkPath).Replace(".unsigned.apk", string.Empty));
        string newSignedName = $"{trueName}.unsigned-aligned-debugSigned";

        File.Move($"{newSignedName}.apk", $"{trueName}.apk", true);
        File.Move($"{newSignedName}.apk.idsig", $"{trueName}.apk.idsig", true);
    }

    private static string GetApkPackageName(string manifestPath)
    {
        XmlDocument xmlDoc = new();
        xmlDoc.Load(manifestPath);

        return xmlDoc.DocumentElement?.Attributes["package"]?.Value
            ?? throw new Exception("Failed to get package name.");
    }

    private async Task ReplaceAllNameInstancesAsync(string apkPath, string searchCompanyName, string replacementName)
    {
        RenameDirectory(Path.Combine(apkPath, "smali\\com", searchCompanyName), replacementName);

        string[] files = Directory.GetFiles(Path.Combine(apkPath, "smali"), "*", SearchOption.AllDirectories);

        _ = await Task.Run(() =>
            _ = Parallel.ForEach(files, async filePath =>
            {
                await ReplaceTextInFileAsync(filePath, searchCompanyName, replacementName);
            })
        );
    }

    private async Task ReplaceObbFiles(string apkSourcePath, string fullSourceApkName, string newApkCompany, string outputDirectory)
    {
        string originalCompanyName = fullSourceApkName.Split('.')[1];
        string? sourceDirectory = Path.GetDirectoryName(apkSourcePath);

        if (sourceDirectory is null)
        {
            LogError("Failed to get APK source directory. No OBB files will be renamed even if they're present.");
            return;
        }

        // Rename OBB file (if there is one)
        string obbDirectory = Path.Combine(sourceDirectory, fullSourceApkName);
        if (Directory.Exists(obbDirectory))
        {
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

            string newApkName = fullSourceApkName.Replace(originalCompanyName, newApkCompany);
            string finalPath = Path.Combine(outputDirectory, fullSourceApkName);

            _ = await Task.Run(() =>
                _ = Parallel.ForEach(Directory.GetFiles(newObbDirectory), filePath =>
                {
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);

                    if (fileNameWithoutExtension.Contains(fullSourceApkName))
                    {
                        Log($"Renaming app file {Path.GetFileName(filePath)}");

                        string newFileName = fileNameWithoutExtension.Replace(originalCompanyName, newApkCompany);
                        string newFilePath = Path.Combine(finalPath, $"{newFileName}{Path.GetExtension(filePath)}");

                        File.Move(filePath, newFilePath, true);
                    }
                })
            );

            RenameDirectory(newObbDirectory, newApkName);
        }
    }

    private static async Task ReplaceTextInFileAsync(string filePath, string searchText, string replaceText)
    {
        string tempFile = $"${Path.GetFileName(filePath)}-{Random.Shared.Next():x00}.tmp.strm";

        using StreamReader input = File.OpenText(filePath);
        using StreamWriter output = new(tempFile);

        try
        {
            string? line;
            while ((line = await input.ReadLineAsync()) is not null)
            {
                await output.WriteLineAsync(line.Replace(searchText, replaceText));
            }
        }
        catch (Exception ex)
        {
            input.Close();
            output.Close();

            File.Delete(tempFile);

            throw;
        }
        finally
        {
            input.Close();
            output.Close();

            File.Delete(filePath);
            File.Move(tempFile, filePath);
        }
    }

    private static void RenameDirectory(string directory, string newName)
    {
        string newFolderPath = Path.Combine(Path.GetDirectoryName(directory), newName);

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
}
