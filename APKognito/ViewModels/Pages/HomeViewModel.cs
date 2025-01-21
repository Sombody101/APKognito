using APKognito.AdbTools;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Exceptions;
using APKognito.Models;
using APKognito.Models.Settings;
using APKognito.Utilities;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Documents;
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

    [ObservableProperty]
    private string _javaExecutablePath = string.Empty;

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
        // For designer
    }

    public HomeViewModel(ISnackbarService _snackbarService)
    {
        Instance = this;

        try
        {
            if (JavaVersionLocator.GetJavaPath(out string? path))
            {
                JavaExecutablePath = path!;
            }
        }
        catch (Exception ex)
        {
            FileLogger.LogException("Failed to pre-fetch Java installation", ex);
        }

        SetSnackbarProvider(_snackbarService);
        SetCurrentLogger();

        kognitoConfig = ConfigurationFactory.GetConfig<KognitoConfig>();
        kognitoCache = ConfigurationFactory.GetConfig<CacheStorage>();
        adbConfig = ConfigurationFactory.GetConfig<AdbConfig>();

        string appDataTools = Path.Combine(App.AppDataDirectory!.FullName, "tools");

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

        string? javaPath = await PrepareForRenaming(files);
        if (javaPath is null)
        {
            goto ChecksFailed;
        }

        Stopwatch elapsedTime = new();
        DispatcherTimer taskTimer = new()
        {
            Interval = TimeSpan.FromSeconds(1),
        };

        taskTimer.Tick += (sender, e) => ElapsedTime = elapsedTime.Elapsed.ToString("hh\\:mm\\:ss");

        elapsedTime.Start();
        taskTimer.Start();

        string[] pendingSession = new string[files.Length];
        List<string> failedJobs = new(files.Length);
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

            string? errorReason = null;
            bool apkFailed = false;

            try
            {
                ApkEditorContext editorContext = new(this, javaPath, sourceApkPath);
                errorReason = await editorContext.RenameApk(cancellationToken);
                apkFailed = errorReason is not null;

                if (!apkFailed && PushAfterRename)
                {
                    await PushRenamedApk(editorContext, cancellationToken);
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

            string? finalName = FinalName;

            if (!apkFailed)
            {
                ++completeJobs;
            }
            else
            {
                failedJobs.Add($"\t{Path.GetFileName(sourceApkPath)}: {errorReason}");
            }

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
            await CleanTempFiles();
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

    private async Task<string?> PrepareForRenaming(string[] files)
    {
        if (files is null || files.Length is 0)
        {
            LogError("No APK files selected!");
            return null;
        }

        string? javaPath = await RenameConditionsMet();
        if (javaPath is null)
        {
            return null;
        }

        Log("Completed all checks.");

        if (!Directory.Exists(OutputDirectory))
        {
            try
            {
                _ = Directory.CreateDirectory(OutputDirectory);
            }
            catch (Exception ex)
            {
                LogError($"Failed to create directory '{OutputDirectory}' ({ex.GetType().Name}). Check for formatting or spelling issues and try again.");
                LogDebug(ex);
                FileLogger.LogException(ex);
                return null;
            }
        }

        // Create a temp directory for the APK(s)
        TempData = Directory.CreateTempSubdirectory("APKognito-");
        Log($"Using temp directory: {TempData.FullName}");

        return javaPath;
    }

    private async Task PushRenamedApk(ApkEditorContext context, CancellationToken cancellationToken)
    {
        var currentDevice = adbConfig.GetCurrentDevice();

        if (currentDevice is null)
        {
            const string error = "Failed to get ADB device profile.";
            LogError(error);
            throw new AdbPushFailedException(JobbedApk, error);
        }

        FileInfo apkInfo = new(context.OutputApkPath);
        Log($"Installing {FinalName} to {currentDevice.DeviceId} ({apkInfo.Length / 1024 / 1024} MB)");

        await AdbManager.WakeDevice();
        await AdbManager.QuickDeviceCommand(@$"install -g ""{apkInfo.FullName}""", token: cancellationToken);

        if (!string.IsNullOrWhiteSpace(context.AssetPath) && Directory.Exists(context.AssetPath) && !cancellationToken.IsCancellationRequested)
        {
            string[] assets = Directory.GetFiles(context.AssetPath);

            string obbDirectory = $"{AdbManager.ANDROID_OBB}/{FinalName}";
            Log($"Pushing {assets.Length} OBB asset(s) to {currentDevice.DeviceId}: {obbDirectory}");

            await AdbManager.QuickDeviceCommand(@$"shell mkdir ""{obbDirectory}""", token: cancellationToken);

            int assetIndex = 0;
            foreach (string file in assets)
            {
                var assetInfo = new FileInfo(file);
                Log($"\tPushing [{++assetIndex}/{assets.Length}]: {assetInfo.Name} ({assetInfo.Length / 1024 / 1024:n0} MB)");

                await AdbManager.QuickDeviceCommand(@$"push ""{file}"" ""{obbDirectory}""", token: cancellationToken);
            }
        }
    }

    private async Task<string?> RenameConditionsMet()
    {
        if (!ValidCompanyName(ApkReplacementName))
        {
            string fixedName = ApkNameFixerRegex().Replace(ApkReplacementName, string.Empty);
            LogError($"The name '{ApkReplacementName}' cannot be used with as the company name of an APK. You can use '{fixedName}' which has all offending characters removed.");
            return null;
        }

        if (PushAfterRename && !await VerifyAdbDevice())
        {
            return null;
        }

        Log("Verifying that Java 8+ and APK tools are installed...");

        if (JavaVersionLocator.GetJavaPath(out string? javaPath) && await VerifyToolInstallation())
        {
            return javaPath;
        }

        return null;
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

    private async Task CleanTempFiles()
    {
        try
        {
            using CancellationTokenSource cts = new();
            cts.CancelAfter(15_000);

            // Remove temp directory
            Log("Cleaning temp directory....");
            await Task.Factory.StartNew(
                path => Directory.Delete((string)path!, true),
                TempData.FullName, cts.Token);
        }
        catch (OperationCanceledException)
        {
            LogWarning("Failed to clear temp files within 15 seconds! Manual cleanup may be required.");
        }
        catch (Exception ex)
        {
            // A file in the temp directory is still being used
            LogWarning($"Failed to cleanup temp files: {ex.Message}");
        }
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