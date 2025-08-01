using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using APKognito.AdbTools;
using APKognito.ApkLib;
using APKognito.ApkLib.Configuration;
using APKognito.ApkLib.Editors;
using APKognito.ApkMod;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Controls;
using APKognito.Controls.Dialogs;
using APKognito.Models;
using APKognito.Utilities;
using APKognito.Utilities.JavaTools;
using APKognito.Utilities.MVVM;
using APKognito.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace APKognito.ViewModels.Pages;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

public partial class HomeViewModel : LoggableObservableObject
{
    public const string DEFAULT_PROP_MESSAGE = "No APK loaded";
    public const string DEFAULT_JOB_MESSAGE = "No jobs started";
    public const string AWAITING_JOB = "Awaiting job";
    public const string CURRENT_ACTION = "Current Action";
    public const char PATH_SEPARATOR = '\n';

    // Configs
    private readonly ConfigurationFactory _configFactory;
    private readonly CacheStorage _kognitoCache;
    private readonly AdbConfig _adbConfig;

    private readonly Progress<ProgressInfo> _renameProgressReporter;

    private readonly JavaVersionCollector _javaVersionCollector;

    // Tool paths
    internal DirectoryInfo _tempData;

    private readonly IContentDialogService _dialogService;

    private CancellationTokenSource? _renameApksCancelationSource;
    private bool _handlingRenameExitDebounce = false;

    [MemberNotNull]
    public static HomeViewModel? Instance { get; private set; } = null!;
    public static bool IsRunningRename => Instance!.RunningJobs;

    public SharedViewModel SharedViewModel { get; }
    public UserRenameConfiguration UserRenameConfiguration { get; }

    #region Properties

    [ObservableProperty]
    public partial bool RunningJobs { get; set; } = false;

    [ObservableProperty]
    public partial bool CanStart { get; set; } = false;

    [ObservableProperty]
    public partial string ApkName { get; set; } = DEFAULT_PROP_MESSAGE;

    [ObservableProperty]
    public partial string OriginalPackageName { get; set; } = DEFAULT_PROP_MESSAGE;

    [ObservableProperty]
    public partial string JobbedApk { get; set; } = DEFAULT_JOB_MESSAGE;

    [ObservableProperty]
    public partial string CurrentActionTitle { get; set; } = CURRENT_ACTION;

    [ObservableProperty]
    public partial string CurrentAction { get; set; } = AWAITING_JOB;

    [ObservableProperty]
    public partial string ElapsedTime { get; set; } = DEFAULT_JOB_MESSAGE;

    [ObservableProperty]
    public partial string CantStartReason { get; set; } = null!;

    [ObservableProperty]
    public partial bool StartButtonVisible { get; set; } = true;

    [ObservableProperty]
    public partial ulong FootprintSizeBytes { get; set; } = 0;

    private string outputDirectory => UserRenameConfiguration.ApkOutputDirectory;

    /// <summary>
    /// A string of all APK paths separated by <see cref="PATH_SEPARATOR"/>
    /// </summary>
    public string FilePath
    {
        get => _kognitoCache?.ApkSourcePath ?? DEFAULT_PROP_MESSAGE;
        set
        {
            _kognitoCache.ApkSourcePath = value;
            OnPropertyChanged(nameof(FilePath));
        }
    }

    public bool CanEdit
    {
        get => SharedViewModel.ConfigurationControlsEnabled;
        set => SharedViewModel.ConfigurationControlsEnabled = value;
    }

    #endregion Properties

    [ActivatorUtilitiesConstructor]
    public HomeViewModel(
        ISnackbarService snackbarService,
        ConfigurationFactory configFactory,
        IContentDialogService dialogService,
        SharedViewModel sharedViewModel,
        JavaVersionCollector javaVersionCollector
    )
    {
        Instance = this;
        SharedViewModel = sharedViewModel;
        UserRenameConfiguration = configFactory.GetConfig<UserRenameConfiguration>();
        base.DisableFileLogging = false;

        _dialogService = dialogService;
        _configFactory = configFactory;
        _kognitoCache = configFactory.GetConfig<CacheStorage>();
        _adbConfig = configFactory.GetConfig<AdbConfig>();
        _javaVersionCollector = javaVersionCollector;

        try
        {
            string appDataTools = Path.Combine(App.AppDataDirectory!.FullName, "tools");
            _ = Directory.CreateDirectory(appDataTools);

            UserRenameConfiguration.BaseToolingPaths = new()
            {
                ApkToolJarPath = Path.Combine(appDataTools, "apktool.jar"),
                ApkToolBatPath = Path.Combine(appDataTools, "apktool.bat"),
                ApkSignerJarPath = Path.Combine(appDataTools, "uber-apk-signer.jar"),
            };
        }
        catch (Exception ex)
        {
            FileLogger.LogException("Failed to pre-fetch Java installation", ex);
        }

        SetSnackbarProvider(snackbarService);
        SetCurrentLogger();

        _renameProgressReporter = new((args) =>
        {
            switch (args.UpdateType)
            {
                case ProgressUpdateType.Content:
                    CurrentAction = args.Data;
                    break;

                case ProgressUpdateType.Title:
                    CurrentActionTitle = args.Data;
                    break;

                case ProgressUpdateType.Reset:
                    CurrentAction = "Awaiting...";
                    CurrentActionTitle = CURRENT_ACTION;
                    break;
            }
        });
    }

    public async Task InitializeAsync()
    {
        if (FilePath.Length is not 0)
        {
            await UpdateFootprintInfoAsync();
        }
    }

    #region Commands

    [RelayCommand]
    private async Task OnStartApkRenameAsync()
    {
        using CancellationTokenSource renameApksCancelationSource = new();
        _renameApksCancelationSource = renameApksCancelationSource;
        CancellationToken cancellationToken = _renameApksCancelationSource.Token;

        try
        {
            await StartPackageRenamingAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            LogError(ex);
        }

        _renameApksCancelationSource = null;
    }

    [RelayCommand]
    private async Task OnCancelApkRenameAsync()
    {
        if (_renameApksCancelationSource is null || _handlingRenameExitDebounce)
        {
            return;
        }

        _handlingRenameExitDebounce = true;

        LogWarning("Attempting to cancel...");

        // Cancel the job(s)
        await _renameApksCancelationSource.CancelAsync();

        _handlingRenameExitDebounce = false;
    }

    [RelayCommand]
    private async Task OnLoadApkAsync()
    {
        try
        {
            await LoadApkAsync();
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            SnackError("Failed to load!", ex.Message);
        }
    }

    [RelayCommand]
    private void OnShowOutputFolder()
    {
        string directory = outputDirectory;
        if (!Directory.Exists(directory))
        {
            LogError($"The directory '{directory}' does not exist. Check the path and try again.");
            return;
        }

        App.OpenDirectory(Path.GetFullPath(directory));
    }

    [RelayCommand]
    private async Task OnUploadPreviousPackageAsync()
    {
        var optionsDialog = new RenamedPackageSelector(_configFactory, _dialogService.GetDialogHost());
        ContentDialogResult dialogResult = await optionsDialog.ShowAsync();

        if (dialogResult is not ContentDialogResult.Primary)
        {
            Log("No package selected.");
            return;
        }

        try
        {
            var renamer = new PackageRenamer(_configFactory, this, _renameProgressReporter);
            await renamer.SideloadPackageAsync(
                optionsDialog.SelectedItem.PackagePath,
                optionsDialog.SelectedItem.Metadata
            );
        }
        catch (Exception e)
        {
            LogError(e);
        }
    }

    [RelayCommand]
    private void OnSaveSettings()
    {
        _configFactory.SaveConfig(UserRenameConfiguration);
        _configFactory.SaveConfig(_kognitoCache);
        Log("Settings saved!");
    }

    [RelayCommand]
    private static void OnNavigateToRenameSettingsPage()
    {
        App.NavigateTo(typeof(RenameConfigurationPage));
    }

    [RelayCommand]
    private async Task OnManualUnpackPackageAsync()
    {
        try
        {
            string? filePath = DirectorySelector.UserSelectFile();

            if (filePath is null)
            {
                return;
            }

            var compressor = new PackageCompressor(new(), UserRenameConfiguration.GetToolingPaths(_javaVersionCollector).Item1, new()
            {
                NewCompanyName = string.Empty,
                ApkAssemblyDirectory = string.Empty,
                ApkSmaliTempDirectory = _tempData?.FullName ?? Path.GetTempPath(),
                FullSourceApkPath = filePath,
                FullSourceApkFileName = Path.GetFileName(filePath),
            });

            string outputDirectory = Path.Combine(Path.GetDirectoryName(filePath)!, $"unpack_{Path.GetFileNameWithoutExtension(filePath)}");
            string apkFileName = Path.GetFileName(filePath);

            Log($"Unpacking {apkFileName}");

            await compressor.UnpackPackageAsync(outputDirectory: outputDirectory);

            Log($"Unpacked {Path.GetFileName(filePath)} into {outputDirectory}");
        }
        catch (Exception ex)
        {
            LogError($"Error: {ex.Message}");
            LogDebug(ex.StackTrace ?? "[NoTrace]");
        }
    }

    [RelayCommand]
    private async Task OnManualPackPackageAsync()
    {
        try
        {
            string? sourceDirectory = DirectorySelector.UserSelectDirectory();

            if (sourceDirectory is null)
            {
                return;
            }

            string packageName = PackageCompressor.GetPackageName(Path.Combine(sourceDirectory, "AndroidManifest.xml"));
            string packageFileName = $"{packageName}.apk";
            string outputFile = Path.Combine(Path.GetDirectoryName(sourceDirectory)!, packageFileName);

            Log($"Packing {sourceDirectory}");

            var compressor = new PackageCompressor(new(), UserRenameConfiguration.GetToolingPaths(_javaVersionCollector).Item1, PackageNameData.Empty, this);
            _ = await compressor.PackPackageAsync(sourceDirectory, outputFile);

            Log($"Packed {Path.GetFileName(sourceDirectory)} into {packageFileName}");
        }
        catch (Exception ex)
        {
            LogError($"Error: {ex.Message}");
            LogDebug(ex.StackTrace ?? "[NoTrace]");
        }
    }

    [RelayCommand]
    private async Task OnManualSignPackageAsync()
    {
        try
        {
            string? filePath = DirectorySelector.UserSelectFile();

            if (filePath is null)
            {
                return;
            }

            Log($"Signing {filePath}");

            var compressor = new PackageCompressor(new(), UserRenameConfiguration.GetToolingPaths(_javaVersionCollector).Item1, PackageNameData.Empty, this);
            string signedPackagePath = Path.Combine(Path.GetDirectoryName(filePath)!, Path.GetDirectoryName(filePath)!);
            await compressor.SignPackageAsync(filePath, signedPackagePath, false);

            Log($"Created {Path.GetFileNameWithoutExtension(filePath)}-aligned-debugSigned.apk");
        }
        catch (Exception ex)
        {
            LogError($"Error: {ex.Message}");
            LogDebug(ex.StackTrace ?? "[NoTrace]");
        }
    }

    [RelayCommand]
    private void OnClearLogBox()
    {
        ClearLogs();

#if DEBUG
        const string logTestString = "Log Test";
        WriteGenericLogLine(logTestString);
        Log(logTestString);
        LogInformation(logTestString);
        LogSuccess(logTestString);
        LogWarning(logTestString);
        LogError(logTestString);
        LogDebug(logTestString);
#endif
    }

    #endregion Commands

    public async ValueTask RefreshValuesAsync()
    {
        await UpdateFootprintInfoAsync();
    }

    public async ValueTask OnRenameCopyCheckedAsync()
    {
        await UpdateFootprintInfoAsync();
    }

    public void UpdateCanStart()
    {
        CanStart = false;

        if (string.IsNullOrWhiteSpace(_kognitoCache.ApkSourcePath))
        {
            CantStartReason = "No input APKs selected. Click 'Select' and pick some.";
            return;
        }

        CantStartReason = null!;

        CanStart = true;
    }

    public string[] GetFilePaths()
    {
        return _kognitoCache?.ApkSourcePath?.Split(PATH_SEPARATOR) ?? [];
    }

    private async Task StartPackageRenamingAsync(CancellationToken token)
    {
        RunningJobs = true;
        CanEdit = false;

        string[]? files = GetFilePaths();

        PackageToolingPaths? toolingPaths = await PrepareForRenamingAsync(files);
        if (toolingPaths is null)
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
            if (token.IsCancellationRequested)
            {
                Log("Job cancellation requested.");
                goto Exit;
            }

            WriteGenericLog($"\n------------------ Job {jobIndex + 1} Start");

            string fileName = Path.GetFileName(sourceApkPath);
            JobbedApk = fileName;

            string tempRenameDirectory = Path.Combine(_tempData.FullName, GetFormattedTimeDirectory(fileName));
            ApkMod.RenameConfiguration renameConfig = new()
            {
                UserRenameConfig = UserRenameConfiguration,
                AdvancedConfig = _configFactory.GetConfig<AdvancedApkRenameSettings>(),
                ToolingPaths = toolingPaths,
                SourcePackagePath = sourceApkPath,
                OutputBaseDirectory = outputDirectory,
                TempDirectory = tempRenameDirectory,
                ReplacementCompanyName = UserRenameConfiguration.ApkNameReplacement
            };

            PackageRenamer renamer = new(_configFactory, this, _renameProgressReporter);
            PackageRenameResult renameResult = await renamer.RenamePackageAsync(renameConfig, UserRenameConfiguration.PushAfterRename, token);

            if (renameResult.Successful)
            {
                ++completeJobs;
            }
            else
            {
                failedJobs.Add($"\t{Path.GetFileName(sourceApkPath)}: {renameResult.ResultStatus}");
            }

            string finalName = !renameResult.Successful
                ? "[Rename Failed]"
                : "[Unknown]";

            pendingSession[jobIndex] = RenameSession.FormatForSerializer(ApkName ?? JobbedApk, finalName, renameResult.Successful);
            ++jobIndex;
            WriteGenericLog($"------------------ Job {jobIndex} End\n");
        }

    Exit:
        Log($"{completeJobs} of {files.Length} APKs were renamed successfully.");
        if (completeJobs != files.Length)
        {
            LogError($"The following APKs failed to be renamed with their error reason:\n{string.Join("\n\t", failedJobs)}");

            // Don't snack the error if they were all canceled
            if (!token.IsCancellationRequested)
            {
                SnackError(
                    "Jobs failed!",
                    $"{completeJobs}/{files.Length} APKs were renamed successfully. See the log box for more details."
                );
            }
        }

        if (UserRenameConfiguration.ClearTempFilesOnRename)
        {
            await CleanTempFilesAsync();
        }

        // Finalize session and write it to the history file
        RenameSession currentSession = new([.. pendingSession], DateTimeOffset.Now.ToUnixTimeSeconds());
        RenameSessionList renameHistory = _configFactory.GetConfig<RenameSessionList>();
        renameHistory.RenameSessions.Add(currentSession);
        _configFactory.SaveConfig(renameHistory);

        elapsedTime.Stop();
        taskTimer.Stop();

        StartButtonVisible = true;

        ResetViewFields();
        JobbedApk = $"Finished {completeJobs}/{files.Length} APKs";

    ChecksFailed:
        RunningJobs = false;
        CanEdit = true;
    }

    private async Task<PackageToolingPaths?> PrepareForRenamingAsync(string[] files)
    {
        if (files is null || files.Length is 0)
        {
            LogError("No APK files selected!");
            return null;
        }

        PackageToolingPaths? toolingPaths = await RenameConditionsMetAsync();
        if (toolingPaths is null)
        {
            return null;
        }

        Log("Completed all checks.");

        if (!Directory.Exists(outputDirectory))
        {
            try
            {
                _ = Directory.CreateDirectory(outputDirectory);
            }
            catch (Exception ex)
            {
                LogError($"Failed to create directory '{outputDirectory}' ({ex.GetType().Name}). Check for formatting or spelling issues and try again.");
                LogDebug(ex);
                FileLogger.LogException(ex);
                return null;
            }
        }

        // Create a temp directory for the APK(s)
        _tempData = Directory.CreateTempSubdirectory("APKognito-");
        _ = DirectoryManager.ClaimDirectory(_tempData.FullName);
        Log($"Using temp directory: {_tempData.FullName}");

        return toolingPaths;
    }

    private async Task<PackageToolingPaths?> RenameConditionsMetAsync()
    {
        if (string.IsNullOrWhiteSpace(UserRenameConfiguration.ApkNameReplacement))
        {
            LogError("The replacement APK name cannot be empty. Use 'apkognito' if you don't know what to replace it with.");
            return null;
        }

        if (!ValidCompanyName(UserRenameConfiguration.ApkNameReplacement))
        {
            string fixedName = ApkNameFixerRegex().Replace(UserRenameConfiguration.ApkNameReplacement, string.Empty);
            LogError($"The name '{UserRenameConfiguration.ApkNameReplacement}' cannot be used with as the company name of an APK. You can use '{fixedName}' which has all offending characters removed.");
            return null;
        }

        if (UserRenameConfiguration.PushAfterRename && !await VerifyAdbDeviceAsync())
        {
            return null;
        }

        Log("Verifying that Java 8+ and APK tools are installed...");

        try
        {
            (PackageToolingPaths toolingPaths, JavaVersionInformation versionInfo) = UserRenameConfiguration.GetToolingPaths(_javaVersionCollector);

            if (await VerifyToolInstallationAsync())
            {
                Log($"Using {versionInfo.JavaType} {versionInfo.Version}");
                return toolingPaths;
            }

            return null;
        }
        catch (JavaVersionCollector.NoJavaInstallationsException noJava)
        {
            FileLogger.LogException(noJava);
            LogError($"Failed to find a valid JDK/JRE installation!\n" +
                "You can install JDK 24 by navigating to the ADB Configuration page and running the installation quick command.\n" +
                "Alternatively, you can run the command `:install-java` in the Console page, or manually install a preferred version.\n" +
                $"\tJDK 24: {AdbManager.JDK_24_INSTALL_EXE_LINK}");
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
        }

        return null;
    }

    private async Task<bool> VerifyToolInstallationAsync()
    {
        try
        {
            bool allSuccess = true;

            string apktoolJarPath = UserRenameConfiguration.BaseToolingPaths.ApkToolJarPath;
            if (!File.Exists(apktoolJarPath))
            {
                Log("Installing Apktool (JAR)...");
                if (!await WebGet.FetchAndDownloadGitHubReleaseAsync(Constants.APKTOOL_JAR_URL_LTST, apktoolJarPath, this))
                {
                    allSuccess = false;
                }
            }

            string apktoolBatPath = UserRenameConfiguration.BaseToolingPaths.ApkToolBatPath;
            if (!File.Exists(apktoolBatPath))
            {
                Log("Installing Apktool (BAT)...");
                if (!await WebGet.DownloadAsync(Constants.APKTOOL_BAT_URL, apktoolBatPath, this))
                {
                    allSuccess = false;
                }
            }

            string apksignerJarPath = UserRenameConfiguration.BaseToolingPaths.ApkSignerJarPath;
            if (!File.Exists(apksignerJarPath))
            {
                Log("Installing ApkSigner...");
                if (!await WebGet.FetchAndDownloadGitHubReleaseAsync(Constants.APL_SIGNER_URL_LTST, apksignerJarPath, this, default, 1))
                {
                    allSuccess = false;
                }
            }

            return allSuccess;
        }
        catch (Exception e)
        {
            LogError($"Unexpected error while dispatching installations: {e.Message}");
            return false;
        }
    }

    private async Task LoadApkAsync()
    {
        OpenFileDialog openFileDialog = new()
        {
            Filter = "APK files (*.apk)|*.apk",
            Multiselect = true,
        };

        bool? result = openFileDialog.ShowDialog();

        if (result is null)
        {
            Log("Failed to get file. Please try again.");
            return;
        }

        if ((bool)result)
        {
            await AddManualFilesAsync(openFileDialog.FileNames);
            await UpdateFootprintInfoAsync();
        }
        else
        {
            Log("Did you forget to select a file from the File Explorer window?");
        }

        UpdateCanStart();
    }

    public async Task AddManualFilesAsync(string[] files)
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
            ApkName = $"{files.Length} packages. (Hover to view)";

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

        await UpdateFootprintInfoAsync();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    private async ValueTask UpdateFootprintInfoAsync()
    {
        FootprintSizeBytes = 0;

        foreach (string file in GetFilePaths())
        {
            if (!File.Exists(file))
            {
                continue;
            }

            FootprintSizeBytes += (ulong)PackageCompressor.CalculateUnpackedApkSize(file, UserRenameConfiguration.CopyFilesWhenRenaming);

            string apkFileName = Path.GetFileNameWithoutExtension(file);
            string obbDirectory = Path.Combine(Path.GetDirectoryName(file)!, apkFileName);

            if (Directory.Exists(obbDirectory))
            {
                try
                {
                    using var cts = new CancellationTokenSource();
                    cts.CancelAfter(TimeSpan.FromSeconds(5));

                    FootprintSizeBytes += await DirectoryManager.DirSizeAsync(obbDirectory, cts.Token);
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

    private async Task<bool> VerifyAdbDeviceAsync()
    {
        switch (await AdbConfigurationViewModel.TryConnectDeviceAsync(_adbConfig))
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
                Log($"Using default device {_adbConfig.CurrentDeviceId}");
                return true;
        }

        return false;
    }

    private async Task CleanTempFilesAsync()
    {
        try
        {
            using CancellationTokenSource tokenSource = new();
            tokenSource.CancelAfter(15_000);

            // Remove temp directory
            Log("Cleaning temp directory....");
            await Task.Factory.StartNew(
                path => Directory.Delete((string)path!, true),
                _tempData.FullName, tokenSource.Token);

            Log("Temp files cleaned.");
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

    private void ResetViewFields()
    {
        OriginalPackageName = DEFAULT_PROP_MESSAGE;
        CurrentActionTitle = CURRENT_ACTION;
        CurrentAction = AWAITING_JOB;
    }

    private static bool ValidCompanyName(string segment)
    {
        return ApkCompanyCheck().IsMatch(segment);
    }

    private static string GetFormattedTimeDirectory(string sourceApkName)
    {
        return $"{sourceApkName}_{DateTime.Now:yyyy-MMMM-dd_h.mm}";
    }

    [GeneratedRegex("[^a-zA-Z0-9]")]
    private static partial Regex ApkNameFixerRegex();

    [GeneratedRegex("[a-z][a-z0-9_]*")]
    private static partial Regex ApkCompanyCheck();
}
