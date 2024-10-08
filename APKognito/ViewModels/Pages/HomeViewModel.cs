using APKognito.Models;
using APKognito.Models.Settings;
using APKognito.Utilities;
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
using Wpf.Ui.Controls;

namespace APKognito.ViewModels.Pages;

#pragma warning disable S1075 // URIs should not be hardcoded

public partial class HomeViewModel : ObservableObject, IViewable, IAntiMvvmRTB
{
    private const string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.6446.71 Safari/537.36";
    private const string defaultPropertyMessage = "No APK loaded";
    private const string defaultJobMessage = "No jobs started";

    public const char PathSeparator = '\n';

    private static readonly HttpClient httpClient = new();
    private readonly KognitoConfig config;

    internal DirectoryInfo TempData;

    // Tool paths
    public readonly string ApktoolJar;
    public readonly string ApktoolBat;
    public readonly string ApksignerJar;

    // By the time this is used anywhere, it will not be null
    public static HomeViewModel? Instance { get; private set; }

    private readonly FontFamily firaRegular = new(new Uri("pack://application:,,,/"), "./Fonts/FiraCode-Medium.ttf#Fira Code Medium");
    private RichTextBox logBox;
    private CancellationTokenSource? _renameApksCancelationSource;

    /*
     * Properties
     */
    #region Properties

    [ObservableProperty]
    private string _startButtonText = "Start";

    [ObservableProperty]
    private bool _runningJobs = false;

    [ObservableProperty]
    private bool _canEdit = true;

    [ObservableProperty]
    private bool _canStart = false;

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
    private string _cantStartReason = string.Empty;

    [ObservableProperty]
    private Visibility _startButtonVisibility = Visibility.Visible;

    [ObservableProperty]
    private Visibility _cancelButtonVisibility = Visibility.Collapsed;

    /// <summary>
    /// Creates a copy of the source files rather than moving them.
    /// Can help with data protection when a renaming session fails as APKognito cannot reverse the changes.
    /// </summary>
    public bool CopyWhenRenaming
    {
        get => config.CopyFilesWhenRenaming;
        set
        {
            config.CopyFilesWhenRenaming = value;
            OnPropertyChanged(nameof(CopyWhenRenaming));
        }
    }

    /// <summary>
    /// A string of all APK paths separated by <see cref="PathSeparator"/>
    /// </summary>
    public string FilePath
    {
        get => config?.ApkSourcePath ?? defaultPropertyMessage;
        set
        {
            config.ApkSourcePath = value;
            OnPropertyChanged(nameof(FilePath));
        }
    }

    /// <summary>
    /// The directory path for all renamed APKs
    /// </summary>
    public string OutputDirectory
    {
        get => config.ApkOutputDirectory;
        set
        {
            config.ApkOutputDirectory = value;
            OnPropertyChanged(nameof(OutputDirectory));
        }
    }

    /// <summary>
    /// The company name that will be used instead of the original APK company name
    /// </summary>
    public string ApkReplacementName
    {
        get => config?.ApkNameReplacement ?? "apkognito";
        set
        {
            config.ApkNameReplacement = value;
            OnPropertyChanged(nameof(ApkReplacementName));
        }
    }

    #endregion Properties

    public HomeViewModel()
    {
        Instance = this;
        config = KognitoSettings.GetSettings();

        string appDataTools = Path.Combine(App.AppData!.FullName, "tools");

        _ = Directory.CreateDirectory(appDataTools);
        ApktoolJar = Path.Combine(appDataTools, "apktoo.jar");
        ApktoolBat = Path.Combine(appDataTools, "apktool.bat");
        ApksignerJar = Path.Combine(appDataTools, "uber-apk-signer.jar");

        httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
    }

    #region Commands

    private bool __handlingRenameExitDebounce = false;

    [RelayCommand]
    private async Task OnStartApkRename()
    {
        if (_renameApksCancelationSource is not null && !__handlingRenameExitDebounce)
        {
            __handlingRenameExitDebounce = true;

            // Cancel the job(s)
            await _renameApksCancelationSource.CancelAsync();
            InvertStartButtonVisibility();

            __handlingRenameExitDebounce = false;
            return;
        }

        using CancellationTokenSource renameApksCancelationSource = new();
        _renameApksCancelationSource = renameApksCancelationSource;
        CancellationToken cancellationToken = _renameApksCancelationSource.Token;

        await RenameApks(cancellationToken);

        _renameApksCancelationSource = null;
    }

    [RelayCommand]
    private async Task OnCancelApkRename()
    {
        // This is fucking stupid. The "Cancel" button will not work otherwise. L WPF.
        await OnStartApkRename();
    }

    [RelayCommand]
    private void OnLoadApk()
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

        UpdateCanStart();
    }

    [RelayCommand]
    private void OnSelectOutputFolder()
    {
        string? oldOutput = OutputDirectory;

        // So it defaults to the Documents folder
        if (!Directory.Exists(oldOutput))
            oldOutput = null;

        OpenFolderDialog openFolderDialog = new()
        {
            Multiselect = false,
            DefaultDirectory = oldOutput ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (openFolderDialog.ShowDialog() is false
            && openFolderDialog.FolderNames.Length is 0)
        {
            return;
        }

        OutputDirectory = openFolderDialog.FolderName;
    }

    [RelayCommand]
    private void OnShowOutputFolder()
    {
        string directory = OutputDirectory;
        if (!Directory.Exists(directory))
        {
            LogError($"The directory '{directory}' does not exist. Check the path and try again.");
            return;
        }

        _ = Process.Start("explorer", Path.GetFullPath(directory));
    }

    [RelayCommand]
    private void OnSaveSettings()
    {
        KognitoSettings.SaveSettings();
        Log("Settings saved!");
    }

    #endregion Commands

    public void UpdateCanStart()
    {
        CanStart = false;

        if (string.IsNullOrWhiteSpace(config.ApkSourcePath))
        {
            CantStartReason = "No input APKs given. Click 'Select' and pick some.";
            return;
        }

        CanStart = true;
    }

    // This is NOT the correct way to do this, but I don't want to setup
    // a convoluted converter for any of this

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

    private async Task RenameApks(CancellationToken cancellationToken)
    {
        RunningJobs = true;
        CanEdit = false;

        string[]? files = GetFilePaths();

        if (files is null || files.Length is 0)
        {
            LogError("No APK files selected!");
            goto ChecksFailed;
        }

        if (!ValidCompanyName(ApkReplacementName))
        {
            string fixedName = ApkNameFixerRegex().Replace(ApkReplacementName, string.Empty);
            LogError($"The name '{ApkReplacementName}' cannot be used with as the company name of an APK. You can use '{fixedName}' which has all offending characters removed.");
            goto ChecksFailed;
        }

        Log("Verifying that Java 8+ and APK tools are installed...");

        if (!VerifyJavaInstallation(out string javaPath) || !await VerifyToolInstallation())
        {
            goto ChecksFailed;
        }

        if (!Directory.Exists(OutputDirectory))
        {
            try
            {
                _ = Directory.CreateDirectory(OutputDirectory);
            }
            catch
            {
                LogError($"Failed to create directory '{OutputDirectory}'. Check for formatting issues and try again.");
                goto ChecksFailed;
            }
        }

        // Create a temp directory for the APK(s)
        TempData = Directory.CreateTempSubdirectory("APKognito-");

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
        string[] pendingSession = new string[files.Length];
        string[] failedJobs = new string[files.Length];
        int completeJobs = 0;

        // Enable a cancel button
        InvertStartButtonVisibility();

        int jobIndex = 0;
        foreach (string sourceApkPath in files)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Log("Job cancellation requested.");
                goto Exit;
            }

            JobbedApk = Path.GetFileName(sourceApkPath);

            ApkEditorContext editorContext = new(this, javaPath, sourceApkPath);

            string? errorReason = null;

            try
            {
                errorReason = await editorContext.RenameApk(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation
            }

            bool apkFailed = errorReason is null;

            if (apkFailed)
            {
                ++completeJobs;
            }
            else
            {
                failedJobs[jobIndex] = $"\t{Path.GetFileName(sourceApkPath)}: {errorReason}";
            }

            pendingSession[jobIndex] = RenameSession.FormatForSerializer(ApkName ?? "[Unknown]", FinalName ?? "[Unknown]", apkFailed);

            ++jobIndex;
        }

    Exit:
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

        elapsedTime.Stop();
        taskTimer.Stop();

        InvertStartButtonVisibility();

        JobbedApk = FinalName = "Finished all APKs";
        StartButtonText = "Start";

    ChecksFailed:
        RunningJobs = false;
        CanEdit = true;
    }

    private async Task<bool> VerifyToolInstallation()
    {
        try
        {
            if (!File.Exists(ApktoolJar))
            {
                Log("Installing Apktool.jar...");
                await FetchAndDownload("https://api.github.com/repos/iBotPeaches/apktool/releases", 0, ApktoolJar);
            }

            if (!File.Exists(ApktoolBat))
            {
                Log("Installing Apktool.bat...");
                await DownloadAsync("https://raw.githubusercontent.com/iBotPeaches/Apktool/master/scripts/windows/apktool.bat", ApktoolBat);
            }

            if (!File.Exists(ApksignerJar))
            {
                Log("Installing ApkSigner.jar");
                await FetchAndDownload("https://api.github.com/repos/patrickfav/uber-apk-signer/releases", 1, ApksignerJar);
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
            javaPath = Path.Combine(javaHome, "bin\\java.exe");
            return true;
        }

        RegistryKey lm = Registry.LocalMachine;

        // Check for JDK via registry
        RegistryKey? javaJdkKey = lm.OpenSubKey("SOFTWARE\\JavaSoft\\JDK");
        if (javaJdkKey is not null)
        {
            if (javaJdkKey.GetValue("CurrentVersion") is not string rawJdkVersion)
            {
                LogWarning($"A JDK installation key was found, but there was no Java version associated with it. Did a Java installation or uninstallation not complete correctly?");
                goto JavaSearchFailed;
            }

            if (!VerifyVersion(rawJdkVersion))
            {
                LogWarning($"JDK installation found with the version {rawJdkVersion}, but it's not Java 9+");
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
            LogError($"Failed to fetch tool: {ex.Message}");
        }
        catch (Exception ex)
        {
            LogError($"An error occurred: {ex.Message}");
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
            Log($"Installing {fileName}");
            await response.Content.CopyToAsync(fileStream);
        }
        catch (HttpRequestException ex)
        {
            LogError($"Unable to download a tool: {ex.Message}");
        }
        catch (Exception ex)
        {
            LogError($"An error occurred: {ex.Message}");
        }
    }

    private void InvertStartButtonVisibility([Optional] bool? forceStartVisible)
    {
        if (StartButtonVisibility is Visibility.Visible || forceStartVisible is false)
        {
            StartButtonVisibility = Visibility.Collapsed;
            CancelButtonVisibility = Visibility.Visible;
        }
        else if (forceStartVisible is true)
        {
            StartButtonVisibility = Visibility.Visible;
            CancelButtonVisibility = Visibility.Collapsed;
        }
    }

    private static bool ValidCompanyName(string segment)
    {
        return ApkCompanyCheck().IsMatch(segment);
    }

    [GeneratedRegex("[^a-zA-Z0-9]")]
    private static partial Regex ApkNameFixerRegex();

    [GeneratedRegex("^[a-zA-Z0-9_]+$")]
    private static partial Regex ApkCompanyCheck();

    public void AntiMvvm_SetRichTextbox(RichTextBox rtb)
    {
        logBox = rtb;
        logBox.Document.FontFamily = firaRegular;
    }
}
