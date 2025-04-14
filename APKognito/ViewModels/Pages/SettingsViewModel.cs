using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using APKognito.Views.Pages.Debugging;
using System.IO;
using System.Reflection;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace APKognito.ViewModels.Pages;

public partial class SettingsViewModel : ViewModel, IViewable
{
    private readonly IContentDialogService contentDialogService;

    private readonly ConfigurationFactory configFactory;
    private readonly UpdateConfig updateConfig;
    private readonly KognitoConfig kognitoConfig;

    private bool _isInitialized = false;

    #region Properties

    [ObservableProperty]
    public partial string FullAppVersion { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AppVersion { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ApplicationTheme CurrentTheme { get; set; } = ApplicationTheme.Unknown;

    [ObservableProperty]
    public partial string ClearedSize { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AppDataPath { get; set; } = string.Empty;

    public bool ClearTempFilesOnRename
    {
        get => kognitoConfig.ClearTempFilesOnRename;
        set
        {
            OnPropertyChanging(nameof(ClearTempFilesOnRename));
            kognitoConfig.ClearTempFilesOnRename = value;
            OnPropertyChanged(nameof(ClearTempFilesOnRename));
        }
    }

    /* Update properties */

    public bool AutomaticUpdatesEnabled
    {
        get => updateConfig.CheckForUpdates;
        set
        {
            OnPropertyChanging(nameof(AutomaticUpdatesEnabled));
            updateConfig.CheckForUpdates = value;
            OnPropertyChanged(nameof(AutomaticUpdatesEnabled));
        }
    }

    public int UpdateDelay
    {
        get => updateConfig.CheckDelay;
        set
        {
            OnPropertyChanging(nameof(UpdateDelay));
            updateConfig.CheckDelay = value;
            OnPropertyChanged(nameof(UpdateDelay));
        }
    }

    #endregion Properties

    public SettingsViewModel()
    {
        // For designer
        OnNavigatedTo();
    }

    public SettingsViewModel(IContentDialogService _contentDialogService, ConfigurationFactory _configFactory)
    {
        contentDialogService = _contentDialogService;

        configFactory = _configFactory;
        updateConfig = configFactory.GetConfig<UpdateConfig>();
        kognitoConfig = configFactory.GetConfig<KognitoConfig>();

        AppDataPath = App.AppDataDirectory.FullName;
    }

    #region Commands

    [RelayCommand]
    private static void OnCreateLogpack()
    {
        _ = CreateLogPack();
    }

    [RelayCommand]
    private void OnSaveUpdatesSettings()
    {
        configFactory.SaveConfig(updateConfig);
    }

    [RelayCommand]
    private static void OnOpenAppData()
    {
        App.OpenDirectory(App.AppDataDirectory!.FullName);
    }

    [RelayCommand]
    private async Task OnUninstallAppCommandAsync(object content)
    {
        ContentDialogResult result = await contentDialogService.ShowSimpleDialogAsync(
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
        App.NavigateTo<LogViewerPage>();
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
        CurrentTheme = ApplicationThemeManager.GetAppTheme();

        Assembly assembly = Assembly.GetExecutingAssembly();
        AssemblyName assemblyName = assembly.GetName();

        string appVersion = App.Version.GetFullVersion(assembly);

        string appName = assemblyName.Name ?? "[Unknown]";
        string fullAppVersion = $"{appVersion} - {assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "[Unknown]"}";
        FullAppVersion = $"{appName} - {fullAppVersion}";
        AppVersion = appVersion;

        _isInitialized = true;
    }

    partial void OnCurrentThemeChanged(ApplicationTheme oldValue, ApplicationTheme newValue)
    {
        ApplicationThemeManager.Apply(newValue);
    }

    public static string CreateLogPack()
    {
        try
        {
            string logpackPath = FileLogger.CreateLogpack();

            MessageBoxResult result = new MessageBox()
            {
                Title = "Logpack Created",
                Content = $"A logpack has been created at:\n{logpackPath}",
                PrimaryButtonText = "Open"
            }.ShowDialogAsync().Result;

            if (result == MessageBoxResult.Primary)
            {
                App.OpenDirectory(Path.GetDirectoryName(logpackPath)!);
            }

            return logpackPath;
        }
        catch (Exception ex)
        {
            _ = new MessageBox()
            {
                Title = "Logpack Failed",
                Content = $"Failed to create logpack.\n\n{ex.Message}",
            }.ShowDialogAsync();
        }

        return string.Empty;
    }
}
