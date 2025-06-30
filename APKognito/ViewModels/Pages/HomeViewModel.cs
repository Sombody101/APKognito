
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using APKognito.AdbTools;
using APKognito.ApkMod;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Controls;
using APKognito.Exceptions;
using APKognito.Helpers;
using APKognito.Models;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using APKognito.Views.Pages;
using Microsoft.Win32;
using Wpf.Ui;

namespace APKognito.ViewModels.Pages;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

public partial class HomeViewModel : LoggableObservableObject
{
    private const string DEFAULT_PROP_MESSAGE = "No APK loaded";
    private const string DEFAULT_JOB_MESSAGE = "No jobs started";
    private const string AWAITING_JOB = "Awaiting job";
    private const string CURRENT_ACTION = "Current Action";
    public const char PATH_SEPARATOR = '\n';

    // Configs
    private readonly ConfigurationFactory configFactory;
    private readonly KognitoConfig kognitoConfig;
    private readonly CacheStorage kognitoCache;
    private readonly AdbConfig adbConfig;

    private static PackageToolingPaths ToolingPaths { get; set; }

    // Tool paths
    internal DirectoryInfo TempData;

    // By the time this is used anywhere, it will not be null
    public static HomeViewModel Instance { get; private set; } = null!;

    private CancellationTokenSource? _renameApksCancelationSource;

    #region Properties

    [ObservableProperty]
    public partial bool RunningJobs { get; set; } = false;

    [ObservableProperty]
    public partial bool CanEdit { get; set; } = true;

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
    public partial long FootprintSizeBytes { get; set; } = 0;

    [ObservableProperty]
    public partial string JavaExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// Creates a copy of the source files rather than moving them.
    /// Can help with data protection when a renaming session fails as APKognito cannot reverse the changes.
    /// </summary>
    [ObservableProperty]
    public partial bool CopyWhenRenaming { get; set; }

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
            if (value == kognitoConfig.ApkNameReplacement)
            {
                return;
            }

            kognitoConfig.ApkNameReplacement = value;
            OnPropertyChanged(nameof(ApkReplacementName));
        }
    }

    #endregion Properties

    public HomeViewModel()
    {
        // For designer
    }

    public HomeViewModel(ISnackbarService _snackbarService, ConfigurationFactory _configFactory)
    {
        DisableFileLogging = false;
        Instance = this;

        try
        {
            if (new JavaVersionLocator().GetJavaPath(out string? path))
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

        configFactory = _configFactory;
        kognitoConfig = configFactory.GetConfig<KognitoConfig>();
        kognitoCache = configFactory.GetConfig<CacheStorage>();
        adbConfig = configFactory.GetConfig<AdbConfig>();
        CopyWhenRenaming = kognitoConfig.CopyFilesWhenRenaming;

        string appDataTools = Path.Combine(App.AppDataDirectory!.FullName, "tools");

        _ = Directory.CreateDirectory(appDataTools);

        ToolingPaths = new()
        {
            ApkToolJarPath = Path.Combine(appDataTools, "apktool.jar"),
            ApkToolBatPath = Path.Combine(appDataTools, "apktool.bat"),
            ApkSignerJarPath = Path.Combine(appDataTools, "uber-apk-signer.jar"),
            JavaExecutablePath = JavaExecutablePath
        };
    }

    public async Task InitializeAsync()
    {
        if (FilePath.Length is not 0)
        {
            await UpdateFootprintInfoAsync();
        }
    }

    #region Commands

    private bool __handlingRenameExitDebounce = false;

    [RelayCommand]
    private async Task OnStartApkRenameAsync()
    {
        using CancellationTokenSource renameApksCancelationSource = new();
        _renameApksCancelationSource = renameApksCancelationSource;
        CancellationToken cancellationToken = _renameApksCancelationSource.Token;

        await StartPackageRenamingAsync(cancellationToken);

        _renameApksCancelationSource = null;
    }

    [RelayCommand]
    private async Task OnCancelApkRenameAsync()
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
        configFactory.SaveConfig(kognitoConfig);
        configFactory.SaveConfig(kognitoCache);
        Log("Settings saved!");
    }

    [RelayCommand]
    private static void OnNavigateToAdvancedSettingsPage()
    {
        App.NavigateTo<AdvancedRenameConfigurationPage>();
    }

    [RelayCommand]
    private async Task OnManualUnpackApkAsync()
    {
        try
        {
            if (!new JavaVersionLocator().GetJavaPath(out string? javaPath, this))
            {
                return;
            }

            foreach (string filePath in GetFilePaths())
            {
                var compressor = new PackageCompressor(ToolingPaths, new()
                {
                    NewCompanyName = string.Empty,
                    ApkAssemblyDirectory = string.Empty,
                    ApkSmaliTempDirectory = TempData?.FullName ?? Path.GetTempPath(),
                    FullSourceApkPath = filePath,
                    FullSourceApkFileName = Path.GetFileName(filePath)
                });

                string outputDirectory = Path.Combine(Path.GetDirectoryName(filePath)!, $"unpack_{Path.GetFileNameWithoutExtension(filePath)}");
                string apkFileName = Path.GetFileName(filePath);

                Log($"Unpacking {apkFileName}");
                await compressor.UnpackPackageAsync(outputDirectory: outputDirectory);

                Log($"Unpacked {Path.GetFileName(filePath)} into {outputDirectory}");
            }
        }
        catch (Exception ex)
        {
            LogError($"Error: {ex.Message}");
            LogDebug(ex.StackTrace ?? "[NoTrace]");
        }
    }

    [RelayCommand]
    private void OnManualPackApk()
    {
        try
        {
            string? directory = DirectorySelector.UserSelectDirectory();

            if (directory is null || !new JavaVersionLocator().GetJavaPath(out string? javaPath, this))
            {
                return;
            }

            throw new NotImplementedException();

            // var context = new ApkEditorContext(new()
            // {
            //     PackageReplaceRegexString = string.Empty,
            //     JavaPath = javaPath!,
            //     SourceApkPath = string.Empty,
            //     TempDirectory = TempData?.FullName ?? Path.GetTempPath(),
            // 
            //     ApktoolJarPath = apktoolJarPath,
            //     ApktoolBatPath = apktoolBatPath,
            //     ApksignerJarPath = apksignerJarPath,
            //     ZipalignPath = zipalignPath,
            // }, this);
            // 
            // string packageName = ApkEditorContext.GetPackageName(Path.Combine(directory, "AndroidManifest.xml"));
            // string packageFileName = $"{packageName}.apk";
            // string outputFile = Path.Combine(Path.GetDirectoryName(directory)!, packageFileName);
            // 
            // Log($"Packing {directory}");
            // await context.PackApkAsync(directory, outputFile);
            // 
            // Log($"Packed {Path.GetFileName(directory)} into {packageFileName}");
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

    partial void OnCopyWhenRenamingChanged(bool value)
    {
        if (!value)
        {
            var result = new MessageBox()
            {
                Title = "Are you sure?",
                Content = "This will remove the entire source APK directory after it is unpacked, meaning if the rename fails, your app will be gone. " +
                    "Only select this option if the saved drive space is worth the risk of losing your app!",
                PrimaryButtonText = "Disable Anyway",
                PrimaryButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Caution,
                CloseButtonText = "Cancel",
            }.ShowDialogAsync().Result;

            if (result is not MessageBoxResult.Primary)
            {
                CopyWhenRenaming = kognitoConfig.CopyFilesWhenRenaming
                    = true;
                return;
            }
        }

        kognitoConfig.CopyFilesWhenRenaming = value;
    }

    public async ValueTask OnRenameCopyCheckedAsync()
    {
        await UpdateFootprintInfoAsync();
    }

    public void UpdateCanStart()
    {
        CanStart = false;

        if (string.IsNullOrWhiteSpace(kognitoCache.ApkSourcePath))
        {
            CantStartReason = "No input APKs selected. Click 'Select' and pick some.";
            return;
        }

        CantStartReason = null!;

        CanStart = true;
    }

    public string[] GetFilePaths()
    {
        return kognitoCache?.ApkSourcePath?.Split(PATH_SEPARATOR) ?? [];
    }

    private async Task StartPackageRenamingAsync(CancellationToken cancellationToken)
    {
        RunningJobs = true;
        CanEdit = false;

        string[]? files = GetFilePaths();

        string? javaPath = await PrepareForRenamingAsync(files);
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

            WriteGenericLog($"\n------------------ Job {jobIndex + 1} Start");

            string fileName = Path.GetFileName(sourceApkPath);
            JobbedApk = fileName;

            AdvancedApkRenameSettings advcfg = configFactory.GetConfig<AdvancedApkRenameSettings>();
            ApkRenameSettings sharedRenameSettings = new()
            {
                SourceApkPath = sourceApkPath,
                OutputBaseDirectory = OutputDirectory,
                JavaPath = javaPath,
                TempDirectory = Path.Combine(TempData.FullName, GetFormattedTimeDirectory(fileName)),
                ApkReplacementName = ApkReplacementName,
                CopyFilesWhenRenaming = kognitoConfig.CopyFilesWhenRenaming,
                ClearTempFilesOnRename = kognitoConfig.ClearTempFilesOnRename,

                PackageReplaceRegexString = advcfg.PackageReplaceRegexString,
                RenameLibs = advcfg.RenameLibs,
                RenameLibsInternal = advcfg.RenameLibsInternal,
                RenameObbsInternal = advcfg.RenameObbsInternal,
                RenameObbsInternalExtras = advcfg.RenameObbsInternalExtras,
                ExtraInternalPackagePaths = advcfg.ExtraInternalPackagePaths,
                AutoPackageEnabled = advcfg.AutoPackageEnabled,
                AutoPackageConfig = advcfg.AutoPackageConfig,
            };

            // Starts the renaming logic
            // Whole lot of method extractions... 100% layer 8 networking issue.
            PackageRenameResult renameResult = await RunPackageRenameAsync(sharedRenameSettings, advcfg, cancellationToken);

            if (renameResult.Successful)
            {
                ++completeJobs;
            }
            else
            {
                failedJobs.Add($"\t{Path.GetFileName(sourceApkPath)}: {renameResult.ResultStatus}");
            }

            string finalName;
                finalName = !renameResult.Successful ? "[Rename Failed]" : "[Unknown]";

            pendingSession[jobIndex] = RenameSession.FormatForSerializer(ApkName ?? JobbedApk, finalName, renameResult.Successful);
            ++jobIndex;
            WriteGenericLog($"------------------ Job {jobIndex} End\n");
        }

    Exit:
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
            await CleanTempFilesAsync();
        }

        // Finalize session and write it to the history file
        RenameSession currentSession = new([.. pendingSession], DateTimeOffset.Now.ToUnixTimeSeconds());
        RenameSessionList renameHistory = configFactory.GetConfig<RenameSessionList>();
        renameHistory.RenameSessions.Add(currentSession);
        configFactory.SaveConfig(renameHistory);

        elapsedTime.Stop();
        taskTimer.Stop();

        StartButtonVisible = true;

        ResetViewFields();
        JobbedApk = $"Finished {completeJobs}/{files.Length} APKs";

    ChecksFailed:
        RunningJobs = false;
        CanEdit = true;
    }

    private async Task<PackageRenameResult> RunPackageRenameAsync(ApkRenameSettings renameSettings, AdvancedApkRenameSettings advConfig, CancellationToken token)
    {
        PackageRenameResult result = null!;

        try
        {
            Progress<ProgressInfo> progress = new((args) =>
            {
                switch (args.UpdateType)
                {
                    case ApkLib.ProgressUpdateType.Content:
                        CurrentAction = args.Data;
                        break;

                    case ApkLib.ProgressUpdateType.Title:
                        CurrentActionTitle = args.Data;
                        break;
                }
            });

            PackageRenamer renamer = new(renameSettings, advConfig, this, progress);
            result = await renamer.RenamePackageAsync(ToolingPaths, token);

            if (Directory.Exists(renameSettings.OutputDirectory))
            {
                DriveUsageViewModel.ClaimDirectory(renameSettings.OutputDirectory);
            }

            if (result.Successful && PushAfterRename)
            {
                await PushRenamedApkAsync(result.OutputLocations, token);
            }
        }
        catch (OperationCanceledException)
        {
            // Handle cancellation
            LogWarning("Job canceled.");
            return new()
            {
                ResultStatus = "Job canceled.",
                Successful = false,
                OutputLocations = new(null!, null, string.Empty)
            };
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            return new()
            {
                ResultStatus = ex.Message,
                Successful = false,
                OutputLocations = new(null!, null, string.Empty)
            };
        }

        return result;
    }

    private async Task<string?> PrepareForRenamingAsync(string[] files)
    {
        if (files is null || files.Length is 0)
        {
            LogError("No APK files selected!");
            return null;
        }

        string? javaPath = await RenameConditionsMetAsync();
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
        DriveUsageViewModel.ClaimDirectory(TempData.FullName);
        Log($"Using temp directory: {TempData.FullName}");

        return javaPath;
    }

    private async Task PushRenamedApkAsync(RenameOutputLocations locations, CancellationToken cancellationToken)
    {
        if (locations.OutputApkPath is null)
        {
            LogError("Renamed APK path is null. Cannot push to device.");
            return;
        }

        AdbDeviceInfo? currentDevice = adbConfig.GetCurrentDevice();

        if (currentDevice is null)
        {
            const string error = "Failed to get ADB device profile.";
            LogError(error);
            throw new AdbPushFailedException(JobbedApk, error);
        }

        FileInfo apkInfo = new(locations.OutputApkPath);

        if (string.IsNullOrWhiteSpace(locations.NewPackageName))
        {
            LogError("Failed to get new package name from location output data. Aborting package upload.");
            return;
        }

        Log($"Installing {locations.NewPackageName} to {currentDevice.DeviceId} ({GBConverter.FormatSizeFromBytes(apkInfo.Length)})");

        await AdbManager.WakeDeviceAsync();
        _ = await AdbManager.QuickDeviceCommandAsync(@$"install -g ""{apkInfo.FullName}""", token: cancellationToken);

        if (string.IsNullOrWhiteSpace(locations.AssetsDirectory)
            || !Directory.Exists(locations.AssetsDirectory)
            || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        string[] assets = Directory.GetFiles(locations.AssetsDirectory);

        string obbDirectory = $"{AdbManager.ANDROID_OBB}/{locations.NewPackageName}";
        Log($"Pushing {assets.Length} OBB asset(s) to {currentDevice.DeviceId}: {obbDirectory}");

        _ = await AdbManager.QuickDeviceCommandAsync(@$"shell [ -d ""{obbDirectory}"" ] && rm -r ""{obbDirectory}""; mkdir ""{obbDirectory}""", token: cancellationToken);

        AddIndent();

        try
        {
            int assetIndex = 0;
            foreach (string file in assets)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var assetInfo = new FileInfo(file);
                Log($"Pushing [{++assetIndex}/{assets.Length}]: {assetInfo.Name} ({GBConverter.FormatSizeFromBytes(assetInfo.Length)})");

                _ = await AdbManager.QuickDeviceCommandAsync(@$"push ""{file}"" ""{obbDirectory}""", token: cancellationToken);
            }
        }
        finally
        {
            ResetIndent();
        }
    }

    private async Task<string?> RenameConditionsMetAsync()
    {
        if (string.IsNullOrWhiteSpace(ApkReplacementName))
        {
            LogError("The replacement APK name cannot be empty. Use 'apkognito' if you don't know what to replace it with.");
            return null;
        }

        if (!ValidCompanyName(ApkReplacementName))
        {
            string fixedName = ApkNameFixerRegex().Replace(ApkReplacementName, string.Empty);
            LogError($"The name '{ApkReplacementName}' cannot be used with as the company name of an APK. You can use '{fixedName}' which has all offending characters removed.");
            return null;
        }

        if (PushAfterRename && !await VerifyAdbDeviceAsync())
        {
            return null;
        }

        Log("Verifying that Java 8+ and APK tools are installed...");

        return new JavaVersionLocator().GetJavaPath(out string? javaPath, this) && await VerifyToolInstallationAsync() ? javaPath : null;
    }

    private async Task<bool> VerifyToolInstallationAsync()
    {
        CancellationToken cToken = CancellationToken.None;

        try
        {
            bool allSuccess = true;

            string apktoolJarPath = ToolingPaths.ApkToolJarPath;
            if (!File.Exists(apktoolJarPath))
            {
                Log("Installing Apktool (JAR)...");
                if (!await WebGet.FetchAndDownloadGitHubReleaseAsync(Constants.APKTOOL_JAR_URL_LTST, apktoolJarPath, this, cToken))
                {
                    allSuccess = false;
                }
            }

            string apktoolBatPath = ToolingPaths.ApkToolBatPath;
            if (!File.Exists(apktoolBatPath))
            {
                Log("Installing Apktool (BAT)...");
                if (!await WebGet.DownloadAsync(Constants.APKTOOL_BAT_URL, apktoolBatPath, this, cToken))
                {
                    allSuccess = false;
                }
            }

            string apksignerJarPath = ToolingPaths.ApkSignerJarPath;
            if (!File.Exists(apksignerJarPath))
            {
                Log("Installing ApkSigner...");
                if (!await WebGet.FetchAndDownloadGitHubReleaseAsync(Constants.APL_SIGNER_URL_LTST, apksignerJarPath, this, cToken, 1))
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

    private async Task LoadApkAsync()
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

#if USE_NEW_APKLIB
            FootprintSizeBytes += PackageCompressor.CalculateUnpackedApkSize(file, CopyWhenRenaming);
#else
            FootprintSizeBytes += ApkEditorContext.CalculateUnpackedApkSize(file, CopyWhenRenaming);
#endif

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

    private async Task<bool> VerifyAdbDeviceAsync()
    {
        switch (await AdbConfigurationViewModel.TryConnectDeviceAsync(adbConfig))
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

    private async Task CleanTempFilesAsync()
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

    [GeneratedRegex("^[a-zA-Z0-9_]+$")]
    private static partial Regex ApkCompanyCheck();
}
