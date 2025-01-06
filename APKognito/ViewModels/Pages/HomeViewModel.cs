using APKognito.AdbTools;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Exceptions;
using APKognito.Models;
using APKognito.Models.Settings;
using APKognito.Utilities;
using Microsoft.Win32;
using System.CodeDom;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Threading;
using Wpf.Ui;

using FontFamily = System.Windows.Media.FontFamily;

namespace APKognito.ViewModels.Pages;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

public partial class HomeViewModel : LoggableObservableObject, IViewable, IAntiMvvmRtb
{
    private const string DEFAULT_PROP_MESSAGE = "No APK loaded";
    private const string DEFAULT_JOB_MESSAGE = "No jobs started";
    public const char PATH_SEPARATOR = '\n';

    private static readonly FontFamily firaRegular = new(new Uri("pack://application:,,,/"), "./Fonts/FiraCode-Medium.ttf#Fira Code Medium");

    // Configs
    private readonly KognitoConfig kognitoConfig;
    private readonly CacheStorage kognitoCache;
    private readonly AdbConfig adbConfig;

    // Tool paths
    internal DirectoryInfo TempData;
    public readonly string
            ApktoolJar,
            ApktoolBat,
            ApksignerJar;

    // By the time this is used anywhere, it will not be null
    public static HomeViewModel Instance { get; private set; } = null!;

    private CancellationTokenSource? _renameApksCancelationSource;

    #region Properties

    [ObservableProperty]
    private bool _runningJobs = false;

    [ObservableProperty]
    private bool _canEdit = true;

    [ObservableProperty]
    private bool _canStart = false;

    [ObservableProperty]
    private string _apkName = DEFAULT_PROP_MESSAGE;

    [ObservableProperty]
    private string _originalPackageName = DEFAULT_PROP_MESSAGE;

    [ObservableProperty]
    private string _finalName = DEFAULT_PROP_MESSAGE;

    [ObservableProperty]
    private string _jobbedApk = DEFAULT_JOB_MESSAGE;

    [ObservableProperty]
    private string _elapsedTime = DEFAULT_JOB_MESSAGE;

    [ObservableProperty]
    private string _cantStartReason = string.Empty;

    [ObservableProperty]
    private bool _startButtonVisible = true;

    [ObservableProperty]
    private long _footprintSizeBytes = 0;

    /// <summary>
    /// Creates a copy of the source files rather than moving them.
    /// Can help with data protection when a renaming session fails as APKognito cannot reverse the changes.
    /// </summary>
    public bool CopyWhenRenaming
    {
        get => kognitoConfig.CopyFilesWhenRenaming;
        set
        {
            kognitoConfig.CopyFilesWhenRenaming = value;
            OnPropertyChanged(nameof(CopyWhenRenaming));
        }
    }

    public bool PushAfterRename
    {
        get => kognitoConfig.PushAfterRename;
        set
        {
            kognitoConfig.PushAfterRename = value;
            OnPropertyChanged(nameof(PushAfterRename));
        }
    }

    /// <summary>
    /// A string of all APK paths separated by <see cref="PATH_SEPARATOR"/>
    /// </summary>
    public string FilePath
    {
        get => kognitoCache?.ApkSourcePath ?? DEFAULT_PROP_MESSAGE;
        set
        {
            kognitoCache.ApkSourcePath = value;
            OnPropertyChanged(nameof(FilePath));
        }
    }

    /// <summary>
    /// The directory path for all renamed APKs
    /// </summary>
    public string OutputDirectory
    {
        get => kognitoConfig.ApkOutputDirectory;
        set
        {
            value = VariablePathResolver.Resolve(value);
            kognitoConfig.ApkOutputDirectory = value;
            OnPropertyChanged(nameof(OutputDirectory));
        }
    }

    /// <summary>
    /// The company name that will be used instead of the original APK company name
    /// </summary>
    public string ApkReplacementName
    {
        get => kognitoConfig.ApkNameReplacement;
        set
        {
            kognitoConfig.ApkNameReplacement = value;
            OnPropertyChanged(nameof(ApkReplacementName));
        }
    }

    #endregion Properties

    public HomeViewModel()
    {
    }

    public HomeViewModel(ISnackbarService _snackbarService)
    {
        Instance = this;

        SetSnackbarProvider(_snackbarService);
        SetCurrentLogger();

        kognitoConfig = ConfigurationFactory.GetConfig<KognitoConfig>();
        kognitoCache = ConfigurationFactory.GetConfig<CacheStorage>();
        adbConfig = ConfigurationFactory.GetConfig<AdbConfig>();

        string appDataTools = Path.Combine(App.AppData!.FullName, "tools");

        _ = Directory.CreateDirectory(appDataTools);
        ApktoolJar = Path.Combine(appDataTools, "apktool.jar");
        ApktoolBat = Path.Combine(appDataTools, "apktool.bat");
        ApksignerJar = Path.Combine(appDataTools, "uber-apk-signer.jar");
    }

    public async Task Initialize()
    {
        if (FilePath.Length is not 0)
        {
            await UpdateFootprintInfo();
        }
    }

    #region Commands

    private bool __handlingRenameExitDebounce = false;

    [RelayCommand]
    private async Task OnStartApkRename()
    {
        using CancellationTokenSource renameApksCancelationSource = new();
        _renameApksCancelationSource = renameApksCancelationSource;
        CancellationToken cancellationToken = _renameApksCancelationSource.Token;

        await RenameApks(cancellationToken);

        _renameApksCancelationSource = null;
    }

    [RelayCommand]
    private async Task OnCancelApkRename()
    {
        if (_renameApksCancelationSource is null || __handlingRenameExitDebounce)
        {
            return;
        }

        __handlingRenameExitDebounce = true;

        LogWarning("Attempting to cancel...");

        // Cancel the job(s)
        await _renameApksCancelationSource.CancelAsync();

        __handlingRenameExitDebounce = false;
    }

    [RelayCommand]
    private async Task OnLoadApk()
    {
        try
        {
            await LoadApk();
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            SnackError("Failed to load!", ex.Message);
        }
    }

    [RelayCommand]
    private void OnSelectOutputFolder()
    {
        string? oldOutput = OutputDirectory;

        // So it defaults to the Documents folder
        if (!Directory.Exists(oldOutput))
        {
            oldOutput = null;
        }

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

        App.OpenDirectory(Path.GetFullPath(directory));
    }

    [RelayCommand]
    private void OnSaveSettings()
    {
        ConfigurationFactory.SaveConfig(kognitoConfig);
        ConfigurationFactory.SaveConfig(kognitoCache);
        Log("Settings saved!");
    }

    [RelayCommand]
    private void OnClearLogBox()
    {
        ClearLogs();

#if DEBUG
        const string logTestString = "Log Test";
        WriteGenericLogLine(logTestString);
        Log(logTestString);
        LogSuccess(logTestString);
        LogWarning(logTestString);
        LogError(logTestString);
        LogDebug(logTestString);
#endif
    }

    #endregion Commands

    public async ValueTask OnRenameCopyChecked()
    {
        await UpdateFootprintInfo();
    }

    public void UpdateCanStart()
    {
        CanStart = false;

        if (string.IsNullOrWhiteSpace(kognitoCache.ApkSourcePath))
        {
            CantStartReason = "No input APKs given. Click 'Select' and pick some.";
            return;
        }

        CanStart = true;
    }

    public string[] GetFilePaths()
    {
        return kognitoCache?.ApkSourcePath?.Split(PATH_SEPARATOR) ?? [];
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

        if (PushAfterRename && !await VerifyAdbDevice())
        {
            goto ChecksFailed;
        }

        Log("Verifying that Java 8+ and APK tools are installed...");

        if (!VerifyJavaInstallation(out string? javaPath) || !await VerifyToolInstallation())
        {
            goto ChecksFailed;
        }

        Log("Completed all checks, ");

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
        Log($"Using temp directory: {TempData.FullName}");

        Stopwatch elapsedTime = new();
        DispatcherTimer taskTimer = new()
        {
            Interval = TimeSpan.FromSeconds(1),
        };

        taskTimer.Tick += (sender, e) => ElapsedTime = elapsedTime.Elapsed.ToString("hh\\:mm\\:ss");

        elapsedTime.Start();
        taskTimer.Start();

        string[] pendingSession = new string[files.Length];
        string[] failedJobs = new string[files.Length];
        int completeJobs = 0;

        StartButtonVisible = false;

        int jobIndex = 0;
        foreach (string sourceApkPath in files)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Log("Job cancellation requested.");
                goto Exit;
            }

            WriteGenericLog($"------------------ Job {jobIndex + 1} Start\n");

            JobbedApk = Path.GetFileName(sourceApkPath);

            ApkEditorContext editorContext = new(this, javaPath!, sourceApkPath);

            string? errorReason = null;
            bool apkFailed = false;

            try
            {
                errorReason = await editorContext.RenameApk(cancellationToken);
                apkFailed = errorReason is not null;

                if (!apkFailed && PushAfterRename)
                {
                    var currentDevice = adbConfig.GetCurrentDevice();

                    if (currentDevice is null)
                    {
                        const string error = "Failed to get ADB device profile.";
                        LogError(error);
                        throw new AdbPushFailedException(JobbedApk, error);
                    }

                    FileInfo apkInfo = new(editorContext.OutputApkPath);
                    Log($"Installing {FinalName} to {currentDevice.DeviceId} ({apkInfo.Length / 1024 / 1024} MB)");

                    await AdbManager.WakeDevice();
                    await AdbManager.QuickDeviceCommand(@$"install -g ""{apkInfo.FullName}""", token: cancellationToken);

                    if (editorContext.AssetPath is not null)
                    {
                        string[] assets = Directory.GetFiles(editorContext.AssetPath);

                        string obbPath = $"/sdcard/Android/{FinalName}";
                        Log($"Pushing {assets.Length} OBB asset(s) to {currentDevice.DeviceId}: {obbPath}");

                        await AdbManager.QuickDeviceCommand(@$"shell mkdir ""{obbPath}""", token: cancellationToken);

                        int assetIndex = 0;
                        foreach (string file in assets)
                        {
                            var assetInfo = new FileInfo(file);
                            Log($"\tPushing [{++assetIndex}/{assets.Length}]: {assetInfo.Name} ({assetInfo.Length / 1024 / 1024:n0} MB)");

                            await AdbManager.QuickDeviceCommand(@$"push ""{file}"" ""{obbPath}""", token: cancellationToken);
                        }
                    }
                }

            }
            catch (OperationCanceledException)
            {
                // Handle cancellation
                LogWarning(errorReason = "Job canceled.");
            }
            catch (Exception ex)
            {
                apkFailed = true;
                errorReason = ex.Message;
                FileLogger.LogException(errorReason, ex);
            }

            if (!apkFailed)
            {
                ++completeJobs;
            }
            else
            {
                failedJobs[jobIndex] = $"\t{Path.GetFileName(sourceApkPath)}: {errorReason}";
            }

            string? finalName = FinalName;
            if (finalName == "Unpacking...")
            {
                finalName = null;
            }

            pendingSession[jobIndex] = RenameSession.FormatForSerializer(
                ApkName ?? JobbedApk,
                finalName ?? (apkFailed
                    ? "[Rename Failed]"
                    : "[Unknown]"),
                apkFailed
            );

            ++jobIndex;
        }

    Exit:
        WriteGenericLog("------------------ Job End\n");
        Log($"{completeJobs} of {files.Length} APKs were renamed successfully.");
        if (completeJobs != files.Length)
        {
            LogError($"The following APKs failed to be renamed with their error reason:\n{string.Join("\n\t", failedJobs)}");
            SnackError(
                "Jobs failed!",
                $"{completeJobs}/{files.Length} APKs were renamed successfully. See the log box for more details."
            );
        }

        if (kognitoConfig.ClearTempFilesOnRename)
        {
            try
            {
                // Remove temp directory
                Log("Cleaning temp directory....");
                await Task.Factory.StartNew(
                    path => Directory.Delete((string)path!, true), 
                    TempData.FullName);
            }
            catch (Exception ex)
            {
                // A file in the temp directory is still being used
                LogWarning($"Failed to cleanup temp files: {ex.Message}");
            }
        }

        // Finalize session and write it to the history file
        RenameSession currentSession = new([.. pendingSession], DateTimeOffset.Now.ToUnixTimeSeconds());
        RenameSessionList renameHistory = ConfigurationFactory.GetConfig<RenameSessionList>();
        renameHistory.RenameSessions.Add(currentSession);
        ConfigurationFactory.SaveConfig(renameHistory);

        elapsedTime.Stop();
        taskTimer.Stop();

        StartButtonVisible = true;

        JobbedApk = FinalName = $"Finished {completeJobs}/{files.Length} APKs";

    ChecksFailed:
        RunningJobs = false;
        CanEdit = true;
    }

    [SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded", Justification = "It's stupid.")]
    private async Task<bool> VerifyToolInstallation()
    {
        CancellationToken cToken = CancellationToken.None;

        try
        {
            bool allSuccess = true;

            if (!File.Exists(ApktoolJar))
            {
                Log("Installing Apktool.jar...");
                if (!await WebGet.FetchAndDownload(Constants.APKTOOL_JAR_URL, ApktoolJar, this, cToken))
                {
                    allSuccess = false;
                }
            }

            if (!File.Exists(ApktoolBat))
            {
                Log("Installing Apktool.bat...");
                if (!await WebGet.DownloadAsync(Constants.APKTOOL_BAT_URL, ApktoolBat, this, cToken))
                {
                    allSuccess = false;
                }
            }

            if (!File.Exists(ApksignerJar))
            {
                Log("Installing ApkSigner.jar");
                if (!await WebGet.FetchAndDownload(Constants.APL_SIGNER_URL, ApksignerJar, this, cToken, 1))
                {
                    allSuccess = false;
                }
            }

            return allSuccess;
        }
        catch (Exception e)
        {
            LogError($"Unexpected error while dispatching installations: {e.Message}");

            // Return false for unknown errors
            return false;
        }
    }

    private async Task LoadApk()
    {
        string openDirectory = Directory.Exists(kognitoCache.LastDialogDirectory)
            ? kognitoCache.LastDialogDirectory
            : "C:\\";

        OpenFileDialog openFileDialog = new()
        {
            Filter = "APK files (*.apk)|*.apk",
            Multiselect = true,
            DefaultDirectory = openDirectory
        };

        bool? result = openFileDialog.ShowDialog();

        if (result is null)
        {
            Log("Failed to get file. Please try again.");
            return;
        }

        if ((bool)result)
        {
            await AddManualFiles(openFileDialog.FileNames);
            await UpdateFootprintInfo();
        }
        else
        {
            Log("Did you forget to select a file from the File Explorer window?");
        }

        UpdateCanStart();
    }

    public async Task AddManualFiles(string[] files, bool verifyTypes = false)
    {
        if (files.Length is 1)
        {
            string selectedFilePath = files[0];

            if (!VerifyFileType(selectedFilePath))
            {
                return;
            }

            FilePath = selectedFilePath;
            string apkName = ApkName = Path.GetFileName(selectedFilePath);
            Log($"Selected {apkName} from: {selectedFilePath}");
        }
        else
        {
            FilePath = string.Join(PATH_SEPARATOR, files);

            StringBuilder sb = new($"Selected {files.Length} APKs\n");

            foreach (string file in files)
            {
                if (!VerifyFileType(file))
                {
                    return;
                }

                _ = sb.Append("\tAt: ").AppendLine(file);
            }

            Log(sb.ToString());
        }

        await UpdateFootprintInfo();

        bool VerifyFileType(string file)
        {
            if (string.Equals(Path.GetExtension(file), ".apk", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            SnackError("Incorrect file types!", $"All files should be APKs! (with the '.apk' file extension)\nOffending file: {file}");
            return false;
        }
    }

    private bool VerifyJavaInstallation(out string? javaPath)
    {
        static bool VerifyVersion(string versionStr)
        {
            // Java versions 8-22
            if (Version.TryParse(versionStr, out Version? jdkVersion) && jdkVersion.Major == 1 && jdkVersion.Minor >= 8)
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

            // Try the registry if this path isn't correct
            if (File.Exists(javaPath))
            {
                return true;
            }

            StringBuilder logBuffer = new();
            bool binExists = Directory.Exists($"{javaHome}\\bin");

            logBuffer.Append("JAVA_HOME is set to '")
                .Append(javaHome).Append("', but does not have java.exe. bin\\: does ");

            if (!binExists)
            {
                logBuffer.Append("not ");
            }

            logBuffer.Append("exist.");

            if (binExists)
            {
                logBuffer.AppendLine(" Files found in bin\\:");
                foreach (var file in Directory.GetFiles(javaHome))
                {
                    logBuffer.Append('\t').AppendLine(Path.GetFileName(file));
                }
            }

            FileLogger.LogError(logBuffer.ToString());
        }

        // Check for JDK via registry
        if (GetKey(Registry.LocalMachine.OpenSubKey("SOFTWARE\\JavaSoft\\Java Runtime Environment"), out javaPath, "JRE")
            || GetKey(Registry.LocalMachine.OpenSubKey("SOFTWARE\\JavaSoft\\JDK"), out javaPath))
        {
            return true;
        }

        LogError("Failed to find a valid JDK/JRE installation!\nYou can install the latest JDK version from here: https://www.oracle.com/java/technologies/downloads/?er=221886#jdk23-windows");
        LogError("If you know you have a Java installation, set your JAVA_HOME environment variable to the proper path for your Java installation.");
        return false;

        bool GetKey(RegistryKey? javaJdkKey, out string? javaPath, string javaType = "JDK")
        {
            javaPath = null;

            if (javaJdkKey is null)
            {
                return false;
            }

            if (javaJdkKey.GetValue("CurrentVersion") is not string rawJdkVersion)
            {
                LogWarning($"A {javaType} installation key was found, but there was no Java version associated with it. Did a Java installation or uninstallation not complete correctly?");
                return false;
            }

            if (!VerifyVersion(rawJdkVersion))
            {
                LogWarning($"{javaType} installation found with the version {rawJdkVersion}, but it's not Java 8+");
                return false;
            }

            string keyPath = (string)javaJdkKey.OpenSubKey(rawJdkVersion)!.GetValue("JavaHome")!;
            string subJavaPath = Path.Combine(keyPath, "bin\\java.exe");

            // This is a VERY rare case
            if (!File.Exists(subJavaPath))
            {
                LogError($"Java version {rawJdkVersion} found, but the Java directory it points to does not exist: {subJavaPath}");
                return false;
            }

            Log($"Using Java version {rawJdkVersion} at {subJavaPath}");
            javaPath = subJavaPath;
            return true;
        }
    }

    private async ValueTask UpdateFootprintInfo()
    {
        FootprintSizeBytes = 0;

        foreach (string file in GetFilePaths())
        {
            if (!File.Exists(file))
            {
                continue;
            }

            FootprintSizeBytes += ApkEditorContext.CalculateUnpackedApkSize(file, CopyWhenRenaming);

            string apkFileName = Path.GetFileNameWithoutExtension(file);
            string obbDirectory = Path.Combine(Path.GetDirectoryName(file)!, apkFileName);

            // Solution for issue: https://github.com/Sombody101/APKognito/issues/4
            if (Directory.Exists(obbDirectory))
            {
                try
                {
                    using var cts = new CancellationTokenSource();
                    cts.CancelAfter(TimeSpan.FromSeconds(5));

                    FootprintSizeBytes += await DriveUsageViewModel.DirSizeAsync(new(obbDirectory), cts.Token);
                }
                catch (TaskCanceledException ex)
                {
                    LogError($"Failed to get asset directory size within time span: {ex.Message}");
                    FileLogger.LogException(ex);
                }
                catch (Exception ex)
                {
                    LogError($"Failed to get asset directory size: {ex.Message}");
                    FileLogger.LogException(ex);
                }
            }
        }
    }

    private async Task<bool> VerifyAdbDevice()
    {
        switch (await AdbConfigurationViewModel.TryConnectDevice(adbConfig))
        {
            case AdbDevicesStatus.NoAdb:
                LogError("Platform tools are not installed. Either:\n\t1. Go to the ADB Console page and run the command ':install-adb'.\nOr:\n\t2. Install platform tools and manually set the path in the ADB Configuration page.");
                return false;

            case AdbDevicesStatus.NoDevices:
                LogError("Failed to find any ADB devices to push renamed APKs to. Connect a device and ensure Developer Mode is enabled.");
                return false;

            case AdbDevicesStatus.TooManyDevices:
                LogError("More than one ADB device was found. Please go to the ADB Configuration page and select a device, then try again.");
                return false;

            case AdbDevicesStatus.DefaultDeviceSelected:
                Log($"Using default device {adbConfig.CurrentDeviceId}");
                return true;
        }

        return false;
    }

    private static readonly List<Run> _runLogBuffer = [];
    public override void AntiMvvm_SetRichTextbox(RichTextBox rtb)
    {
        rtb.Document.FontFamily = firaRegular;

        // Dump all logs
        ((Paragraph)rtb.Document.Blocks.LastBlock).Inlines.AddRange(_runLogBuffer);
        _runLogBuffer.Clear();

        base.AntiMvvm_SetRichTextbox(rtb);
    }

    private static bool ValidCompanyName(string segment)
    {
        return ApkCompanyCheck().IsMatch(segment);
    }

    [GeneratedRegex("[^a-zA-Z0-9]")]
    private static partial Regex ApkNameFixerRegex();

    [GeneratedRegex("^[a-zA-Z0-9_]+$")]
    private static partial Regex ApkCompanyCheck();
}