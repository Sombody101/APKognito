using System.IO;
using System.Reflection;
using APKognito.Cli;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Controls.Dialogs;
using APKognito.Services;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using APKognito.Views.Pages.Debugging;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace APKognito.ViewModels.Pages;

public partial class SettingsViewModel : ViewModel, IViewable
{
    private readonly IContentDialogService _contentDialogService;
    private readonly ConfigurationFactory _configFactory;
    private readonly UpdateConfig _updateConfig;
    private readonly UserRenameConfiguration _kognitoConfig;
    private readonly UserThemeConfig _userThemeConfig;
    private readonly CacheStorage _cacheStorage;

    private bool _isInitialized = false;

    #region Properties

    [ObservableProperty]
    public partial string FullAppVersion { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AppVersion { get; set; } = string.Empty;

    public ApplicationTheme CurrentTheme
    {
        get => _userThemeConfig.AppTheme;
        set
        {
            if (_userThemeConfig.AppTheme == value)
            {
                return;
            }

            _userThemeConfig.AppTheme = value;
            ApplicationThemeManager.Apply(value);
            OnPropertyChanged(nameof(CurrentTheme));
        }
    }

    public bool UseAccent
    {
        get => _userThemeConfig.UseSystemAccent;
        set
        {
            if (_userThemeConfig.UseSystemAccent == value)
            {
                return;
            }

            _userThemeConfig.UseSystemAccent = value;
            _userThemeConfig.ApplyUserTheme();
            OnPropertyChanged(nameof(UseAccent));
        }
    }

    public WindowBackdropType WindowStyle
    {
        get => _userThemeConfig.WindowStyle;
        set
        {
            if (_userThemeConfig.WindowStyle == value)
            {
                return;
            }

            _userThemeConfig.WindowStyle = value;
            OnPropertyChanged(nameof(WindowStyle));
            _userThemeConfig.NotifyChanged();
        }
    }

    [ObservableProperty]
    public partial string ClearedSize { get; set; } = string.Empty;

    [CalledByGenerated]
    public string AppDataPath => App.AppDataDirectory.FullName
#if DEBUG
        .Redact("user")
#endif
        ;

    public bool ClearTempFilesOnRename
    {
        get => _kognitoConfig.ClearTempFilesOnRename;
        set
        {
            _kognitoConfig.ClearTempFilesOnRename = value;
            OnPropertyChanged(nameof(ClearTempFilesOnRename));
        }
    }

    public LogLevel MinimumLogLevel
    {
        get => _cacheStorage.MinimumLogLevel;
        set
        {
            _cacheStorage.MinimumLogLevel = value;
            OnPropertyChanged(nameof(MinimumLogLevel));
        }
    }

    public bool LogExceptionsToView
    {
        get => _cacheStorage.LogExceptionsToView;
        set
        {
            _cacheStorage.LogExceptionsToView = value;
            OnPropertyChanged(nameof(LogExceptionsToView));
        }
    }

    /* Update properties */

    public bool AutomaticUpdatesEnabled
    {
        get => _updateConfig.CheckForUpdates;
        set
        {
            _updateConfig.CheckForUpdates = value;
            OnPropertyChanged(nameof(AutomaticUpdatesEnabled));
        }
    }

    public int UpdateDelay
    {
        get => _updateConfig.CheckDelay;
        set
        {
            _updateConfig.CheckDelay = value;
            OnPropertyChanged(nameof(UpdateDelay));
        }
    }

    #endregion Properties

    public SettingsViewModel(IContentDialogService contentDialogService, ConfigurationFactory configFactory)
    {
        _contentDialogService = contentDialogService;

        _configFactory = configFactory;
        _updateConfig = configFactory.GetConfig<UpdateConfig>();
        _kognitoConfig = configFactory.GetConfig<UserRenameConfiguration>();
        _userThemeConfig = configFactory.GetConfig<UserThemeConfig>();
        _cacheStorage = configFactory.GetConfig<CacheStorage>();
    }

    public SettingsViewModel()
    {
        _configFactory = null!;
        _updateConfig = null!;
        _kognitoConfig = null!;
        _contentDialogService = null!;
        _userThemeConfig = null!;
        _cacheStorage = null!;

        // For designer
        // OnNavigatedTo();
    }

    #region Commands

    [RelayCommand]
    private async Task OnCreateLogpackAsync()
    {
        await CreateLogPackAsync(_contentDialogService);
    }

    [RelayCommand]
    private void OnSaveUpdatesSettings()
    {
        _configFactory.SaveConfig(_updateConfig);
    }

    [RelayCommand]
    private static void OnRunUpdateCheck()
    {
        App.GetService<AutoUpdaterService>()?.ForceUpdateCheck();
    }

    [RelayCommand]
    private static void OnOpenAppData()
    {
        App.OpenDirectory(App.AppDataDirectory!.FullName);
    }

    [RelayCommand]
    private async Task OnUninstallAppCommandAsync(object content)
    {
        ContentDialogResult result = await _contentDialogService.ShowSimpleDialogAsync(
            new SimpleContentDialogCreateOptions()
            {
                Title = "Uninstall APKognito?",
                Content = content,
                PrimaryButtonText = "Uninstall",
                CloseButtonText = "Cancel",
            }
        );

        if (result is not ContentDialogResult.Primary)
        {
            return;
        }
    }

    [RelayCommand]
    private static void OnNavigateToLogViewer()
    {
        App.NavigateTo(typeof(LogViewerPage));
    }

    [RelayCommand]
    private static void OnStartDebugConsole()
    {
        CliMain.CreateConsole();
    }

    #endregion Commands

    public override void OnNavigatedTo()
    {
        if (!_isInitialized)
        {
            InitializeViewModel();
        }
    }

    private void InitializeViewModel()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        AssemblyName assemblyName = assembly.GetName();

        string appVersion = App.Version.GetFullVersion(assembly);

        string appName = assemblyName.Name ?? "[Unknown]";
        string informationVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "[Unknown]";
        string fullAppVersion = $"{appVersion} - {informationVersion}";
        FullAppVersion = $"{appName} - {fullAppVersion}";
        AppVersion = appVersion;

        _isInitialized = true;
    }

    public static async Task<string?> CreateLogPackAsync(IContentDialogService dialogService)
    {
        try
        {
            var optionsDialog = new LogpackCreatorDialog(dialogService.GetDialogHost());
            ContentDialogResult dialogResult = await optionsDialog.ShowAsync();

            if (dialogResult is not ContentDialogResult.Primary)
            {
                return null;
            }

            string logpackPath = await FileLogger.CreateLogpackAsync(optionsDialog.IncludeCrashLogs);

            MessageBoxResult result = await new MessageBox()
            {
                Title = "Logpack Created",
                Content = $"A logpack has been created at:\n{logpackPath}",
                PrimaryButtonText = "Open"
            }.ShowDialogAsync();

            if (result is MessageBoxResult.Primary)
            {
                App.OpenDirectory(Path.GetDirectoryName(logpackPath)!);
            }

            return logpackPath;
        }
        catch (Exception ex)
        {
            await new MessageBox()
            {
                Title = "Logpack Failed",
                Content = $"Failed to create logpack.\n\n{ex.Message}",
            }.ShowDialogAsync();
        }

        return null;
    }
}
