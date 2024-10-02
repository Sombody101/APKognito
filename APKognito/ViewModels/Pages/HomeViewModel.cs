using APKognito.Models;
using APKognito.Models.Settings;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using Wpf.Ui.Controls;

namespace APKognito.ViewModels.Pages;

#pragma warning disable S1075 // URIs should not be hardcoded

public partial class HomeViewModel : ObservableObject, IViewable
{
    private const string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.6446.71 Safari/537.36";
    private const string defaultPropertyMessage = "No APK loaded";
    private const string defaultJobMessage = "No jobs started";
    public const char PathSeparator = '\n';

    public readonly string AppData;
    private DirectoryInfo TempData;

    private static readonly HttpClient httpClient = new();
    private readonly KognitoConfig config;

    // Tool paths
    private readonly string apktoolJar;
    private readonly string apktoolBat;
    private readonly string apksignerJar;

    // By the time this is used anywhere, it will not be null
    public static HomeViewModel? Instance { get; private set; }


    private RichTextBox logBox;
    private readonly FontFamily firaRegular = new(new Uri("pack://application:,,,/"), "./Fonts/FiraCode-Medium.ttf#Fira Code Medium");

    // This is NOT the correct way to do this, but I don't want to setup
    // a convoluted converter for any of this
    public void AntiMvvm_ConfigureLogger(RichTextBox _logBox)
    {
        logBox = _logBox;
        logBox.Document.FontFamily = firaRegular;
    }

    public void WriteGenericLog(string text, [Optional] Brush color)
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

    /*
     * Properties
     */
    #region Properties

    [ObservableProperty]
    private bool _runningJobs = false;

    [ObservableProperty]
    private bool _canEdit = true;

    [ObservableProperty]
    private string _apkName = defaultPropertyMessage;

    [ObservableProperty]
    private string _originalPackageName = defaultPropertyMessage;

    [ObservableProperty]
    private string _finalName = defaultPropertyMessage;

    [ObservableProperty]
    private string _jobbedApk = defaultJobMessage;

    [ObservableProperty]
    private string _elapsedTime = defaultJobMessage;

    [ObservableProperty]
    private bool _canStart = false;

    public string FilePath
    {
        get => config?.ApkSourcePath ?? defaultPropertyMessage;
        set
        {
            config.ApkSourcePath = value;
            OnPropertyChanged(nameof(FilePath));
        }
    }

    public string[]? GetFilePaths()
    {
        return config?.ApkSourcePath?.Split(PathSeparator);
    }

    public string OutputPath
    {
        get => config?.ApkOutputDirectory ?? Path.Combine(AppData, "output");
        set
        {
            config.ApkOutputDirectory = value;
            OnPropertyChanged();
        }
    }

    public string ApkReplacementName
    {
        get => config?.ApkNameReplacement ?? "apkognito";
        set => config.ApkNameReplacement = value;
    }

    #endregion Properties

    public HomeViewModel()
    {
        Instance = this;
        config = KognitoSettings.GetSettings();

        AppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), nameof(APKognito));

        apktoolJar = Path.Combine(AppData, "apktoo.jar");
        apktoolBat = Path.Combine(AppData, "apktool.bat");
        apksignerJar = Path.Combine(AppData, "uber-apk-signer.jar");

        httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
    }

    [RelayCommand]
    private async Task StartApkRename()
    {
        string[]? files = GetFilePaths();

        if (files is null || files.Length is 0)
        {
            LogError("No APK files selected!");
            return;
        }

        if (!ValidCompanyName(ApkReplacementName))
        {
            string fixedName = ApkNameFixerRegex().Replace(ApkReplacementName, string.Empty);
            LogError($"The name '{ApkReplacementName}' cannot be used with as the company name of an APK. You can use '{fixedName}' which has all offending characters removed.");
            return;
        }

        Log("Verifying that Java 8+ and APK tools are installed...");

        if (!VerifyJavaInstallation(out string javaPath) || !await VerifyToolInstallation())
        {
            return;
        }

        // Create a temp directory for the APK(s)
        TempData = Directory.CreateTempSubdirectory("APKognito-");
        _ = Directory.CreateDirectory(OutputPath);

        RunningJobs = true;
        CanEdit = false;

        Stopwatch elapsedTime = new();
        DispatcherTimer taskTimer = new()
        {
            Interval = TimeSpan.FromSeconds(1),
        };

        taskTimer.Tick += (sender, e) =>
        {
            ElapsedTime = elapsedTime.Elapsed.ToString("hh\\:mm\\:ss");
        };

        elapsedTime.Start();
        taskTimer.Start();

        List<RenameSession> renameHistory = RenameSessionManager.GetSessions();
        List<string> pendingSession = [];

        List<string> failedJobs = [];
        int completeJobs = 0;
        foreach (string apkTarget in files)
        {
            JobbedApk = Path.GetFileName(apkTarget);

            string? errorReason = await RenameApk(javaPath, apkTarget, OutputPath);
            bool apkFailed = errorReason is null;

            if (apkFailed)
            {
                ++completeJobs;
            }
            else
            {
                failedJobs.Add($"\t{Path.GetFileName(apkTarget)}: {errorReason}");
            }

            pendingSession.Add(RenameSession.FormatForSerializer(ApkName ?? "[Unknown]", FinalName ?? "[Unknown]", apkFailed));
        }

        WriteGenericLog("------------------\n");
        Log($"{completeJobs} of {files.Length} APKs were renamed successfully.");
        if (completeJobs != files.Length)
        {
            LogError($"The following APKs failed to be renamed with their error reason:\n{string.Join("\n\t", failedJobs)}");
        }

        // Finalize session and write it to the history file
        RenameSession currentSession = new([.. pendingSession], DateTimeOffset.Now.ToUnixTimeSeconds());
        renameHistory.Add(currentSession);
        RenameSessionManager.SaveSessions();

        JobbedApk = FinalName = "Finished all APKs";
        RunningJobs = false;
        CanEdit = true;
        elapsedTime.Stop();
        taskTimer.Stop();
    }

    [RelayCommand]
    private void LoadApk()
    {
        OpenFileDialog openFileDialog = new()
        {
            Filter = "APK files (*.apk)|*.apk",
            Multiselect = true,
            DefaultDirectory = config.LastDialogDirectory
        };

        bool? result = openFileDialog.ShowDialog();

        if (result is null)
        {
            Log("Failed to get file. Please try again.");
            return;
        }

        if ((bool)result)
        {
            string[] selectedFilePaths = openFileDialog.FileNames;

            if (selectedFilePaths.Length is 1)
            {
                string selectedFilePath = selectedFilePaths[0];
                FilePath = selectedFilePath;
                string apkName = ApkName = Path.GetFileName(selectedFilePath);
                Log($"Selected {apkName} from: {selectedFilePath}");
            }
            else
            {
                FilePath = string.Join(PathSeparator, selectedFilePaths);

                StringBuilder sb = new($"Selected {selectedFilePaths.Length} APKs\n");

                foreach (string str in selectedFilePaths)
                {
                    _ = sb.Append("\tAt: ").AppendLine(str);
                }

                Log(sb.ToString());
            }
        }
        else
        {
            Log("Did you forget to select a file from the File Explorer window?");
        }
    }

    [RelayCommand]
    private void ShowOutputFolder()
    {
        string directory = OutputPath;
        if (!ValidDirectory(directory))
        {
            return;
        }

        _ = Process.Start("explorer", Path.GetFullPath(directory));
    }

    [RelayCommand]
    private void SaveSettings()
    {
        KognitoSettings.SaveSettings();
        Log("Settings saved!");
    }

    private async Task<string?> RenameApk(string java, string sourceApk, string output)
    {
        try
        {
            FinalName = "Unpacking...";
            string tempData = TempData.FullName;
            string sourceApkName = Path.GetFileName(sourceApk);
            string apkTempFolder = Path.Combine(tempData, $"{sourceApkName[..^4]}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");

            _ = Directory.CreateDirectory(apkTempFolder);

            WriteGenericLog("------------------\n");

            // Unpack
            Log($"Unpacking {sourceApkName}");
            await UnpackApk(java, sourceApk, apkTempFolder);

            // Replace package name
            Log("Getting package name...");
            string packageName = GetApkPackageName(Path.Combine(apkTempFolder, "AndroidManifest.xml"));

            string[] split = packageName.Split('.');
            string oldCompanyName = split[1];
            split[1] = ApkReplacementName;

            string newPackageName = string.Join('.', split);
            FinalName = newPackageName;

            Log($"Changing '{packageName}'  →  '{newPackageName}'");
            await Task.WhenAll(
                ReplaceTextInFileAsync(Path.Combine(apkTempFolder, "AndroidManifest.xml"), oldCompanyName, ApkReplacementName),
                ReplaceTextInFileAsync(Path.Combine(apkTempFolder, "apktool.yml"), oldCompanyName, ApkReplacementName)
            );

            await ReplaceAllNameInstancesAsync(sourceApk, apkTempFolder, oldCompanyName, ApkReplacementName, output);

            throw new Exception("Test");

            // Repack
            Log("Packing APK...");
            string unsignedApk = await PackApk(java, apkTempFolder, newPackageName);

            // Sign
            Log("Singing APK...");
            await SignApkTool(java, unsignedApk, Path.Combine(OutputPath, $"{newPackageName}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}"));

            // Copy to output and cleanup
            Log($"Finished APK {newPackageName}.");

            Log("Cleaning up...");
            Directory.Delete(apkTempFolder, true);
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

    private async Task<bool> VerifyToolInstallation()
    {
        _ = Directory.CreateDirectory(AppData);

        try
        {
            if (!File.Exists(apktoolJar))
            {
                Log("Installing Apktool.jar...");
                await FetchAndDownload("https://api.github.com/repos/iBotPeaches/apktool/releases", 0, apktoolJar);
            }

            if (!File.Exists(apktoolBat))
            {
                Log("Installing Apktool.bat...");
                await DownloadAsync("https://raw.githubusercontent.com/iBotPeaches/Apktool/master/scripts/windows/apktool.bat", apktoolBat);
            }

            if (!File.Exists(apksignerJar))
            {
                Log("Installing ApkSigner.jar");
                await FetchAndDownload("https://api.github.com/repos/patrickfav/uber-apk-signer/releases", 1, apksignerJar);
            }
        }
        catch
        {
            // Return false if an exception was thrown while installing the tools
            return false;
        }

        return true;
    }

    private bool VerifyJavaInstallation(out string javaPath)
    {
        static bool VerifyVersion(string versionStr)
        {
            // Java versions 9-22
            if (Version.TryParse(versionStr, out Version? jdkVersion) && jdkVersion.Major >= 18)
            {
                return true;
            }

            // Formatting for Java versions 23+ (or the JAVA_HOME path)
            return int.TryParse(versionStr.Split('.')[0], out int major) && major >= 9;
        }

        // Check with the environment variable first
        string? javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");

        if (!string.IsNullOrWhiteSpace(javaHome)
           && VerifyVersion( /* Remove 'jdk-' from the folder */ Path.GetFileName(javaHome)[4..])
           && Directory.Exists(javaHome))
        {
            javaPath = Path.Combine(javaHome, "\\bin\\java.exe");
            return true;
        }

        RegistryKey lm = Registry.LocalMachine;

        // Check for JDK via registry
        RegistryKey? javaJdkKey = lm.OpenSubKey("SOFTWARE\\JavaSoft\\JDK");
        if (javaJdkKey is not null)
        {
            if (javaJdkKey.GetValue("CurrentVersion") is not string rawJdkVersion)
            {
                LogError($"A JDK installation key was found, but there was no Java version associated with it. Did a Java installation or uninstallation not complete correctly?");
                goto JavaSearchFailed;
            }

            if (!VerifyVersion(rawJdkVersion))
            {
                LogError($"JDK installation found with the version {rawJdkVersion}, but it's not Java 9+");
                goto JavaSearchFailed;
            }

            string keyPath = (string)javaJdkKey.OpenSubKey(rawJdkVersion)!.GetValue("JavaHome")!;
            javaPath = Path.Combine(keyPath, "bin\\java.exe");

            // This is a VERY rare case
            if (!File.Exists(javaPath))
            {
                LogError($"Java version {rawJdkVersion} found, but the Java directory it points to does not exist: {javaPath}");
                return false;
            }

            Log($"Using Java version {rawJdkVersion} at {javaPath}");
            return true;
        }

    // A JRE check will be implemented. Eventually...

    JavaSearchFailed:
        LogError("Failed to find a valid JDK installation!\nYou can install the latest JDK version from here: https://www.oracle.com/java/technologies/downloads/?er=221886#jdk23-windows");
        javaPath = string.Empty;
        return false;
    }

    private async Task UnpackApk(string javaPath, string sourceApk, string outputDirectory)
    {
        using Process process = CreateJavaProcess(javaPath, $"-jar \"{apktoolJar}\" -f d \"{sourceApk}\" -o \"{outputDirectory}\"");

        _ = process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode is 0)
        {
            return;
        }

        string error = await process.StandardError.ReadToEndAsync();
        LogError($"Failed to unpack {sourceApk}. Error: {error}");
        throw new Exception(error);
    }

    public async Task<string> PackApk(string javaPath, string directoryPath, string newPackageName)
    {
        string apkName = $"{newPackageName}.unsigned.apk";
        string outputApkName = $"{Path.Combine(directoryPath, apkName)}";

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

    private static async Task ReplaceAllNameInstancesAsync(string apkSourcePath, string apkPath, string searchCompanyName, string replacementName, string outputDirectory)
    {
        static void RenameDirectory(string directory, string newName)
        {
            string newFolderPath = Path.Combine(Path.GetDirectoryName(directory), newName);

            Directory.Move(directory, newFolderPath);
        }

        RenameDirectory(Path.Combine(apkPath, "smali\\com", searchCompanyName), replacementName);

        // Rename OBB file (if there is one)
        string obbDirectory = Path.Combine(apkSourcePath, searchCompanyName);
        if (Directory.Exists(obbDirectory))
        {
            _ = await Task.Run(() =>
                _ = Parallel.ForEach(Directory.GetFiles(obbDirectory), filePath =>
                {
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);

                    if (fileNameWithoutExtension.Contains(searchCompanyName))
                    {
                        string newFileName = fileNameWithoutExtension.Replace(searchCompanyName, replacementName);
                        string newFilePath = Path.Combine(obbDirectory, newFileName, Path.GetExtension(filePath));

                        File.Move(filePath, newFilePath);
                    }
                })
            );

            RenameDirectory(obbDirectory, Path.Combine(obbDirectory, replacementName));

            // Move the renamed OBB to the output
            Directory.Move(obbDirectory, outputDirectory);
        }

        string[] files = Directory.GetFiles(Path.Combine(apkPath, "smali"), "*", SearchOption.AllDirectories);

        _ = await Task.Run(() =>
            _ = Parallel.ForEach(files, async filePath =>
            {
                await ReplaceTextInFileAsync(filePath, searchCompanyName, replacementName);
            })
        );
    }

    private static async Task ReplaceTextInFileAsync(string filePath, string searchText, string replaceText)
    {
        string tempFile = $"${Path.GetFileName(filePath)}-{Random.Shared.Next():x00}.tmp.strm";

        using StreamReader input = File.OpenText(filePath);
        using StreamWriter output = new(tempFile);

        string? line;
        while ((line = await input.ReadLineAsync()) is not null)
        {
            await output.WriteLineAsync(line.Replace(searchText, replaceText));
        }

        input.Close();
        output.Close();

        File.Delete(filePath);
        File.Move(tempFile, filePath);
    }

    private async Task FetchAndDownload(string url, int num, string name)
    {
        try
        {
            HttpResponseMessage response = await httpClient.GetAsync(url);

            _ = response.EnsureSuccessStatusCode();

            string jsonResult = await response.Content.ReadAsStringAsync();
            dynamic dynObj = JsonConvert.DeserializeObject(jsonResult)!;
            string browser_url = dynObj[0].assets[num].browser_download_url;

            await DownloadAsync(browser_url, name);
        }
        catch (HttpRequestException ex)
        {
            Log($"An HttpRequestException occurred! {ex.Message}");
        }
        catch (Exception ex)
        {
            Log($"An error occurred! {ex.Message}");
        }
    }

    private async Task DownloadAsync(string url, string name)
    {
        try
        {
            string fileName = Path.GetFileName(name);
            Log($"Fetching {fileName}");
            using HttpResponseMessage response = await httpClient.GetAsync(url);
            _ = response.EnsureSuccessStatusCode();

            using FileStream fileStream = File.Create(name);
            Log($"Writing {fileName}");
            await response.Content.CopyToAsync(fileStream);
        }
        catch (HttpRequestException ex)
        {
            Log($"Unable to download a tool! {ex.Message}");
        }
        catch (Exception ex)
        {
            Log($"An error occurred! {ex.Message}");
        }
    }

    private static bool ValidCompanyName(string segment)
    {
        return ApkCompanyCheck().IsMatch(segment);
    }

    public bool ValidDirectory(string path)
    {
        string directory = OutputPath;
        if (!Directory.Exists(directory))
        {
            LogError($"The directory '{directory}' does not exist. Check the path and try again.");
            return false;
        }

        return true;
    }

    private static Process CreateJavaProcess(string javaPath, string arguments)
    {
        return new()
        {
            StartInfo = new()
            {
                FileName = javaPath,
                Arguments = arguments,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
    }

    [GeneratedRegex("[^a-zA-Z0-9]")]
    private static partial Regex ApkNameFixerRegex();

    [GeneratedRegex("^[a-zA-Z0-9_]+$")]
    private static partial Regex ApkCompanyCheck();
}
