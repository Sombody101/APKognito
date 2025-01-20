using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Models.Settings;
using APKognito.Utilities;
using System.IO;
using System.Reflection;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace APKognito.ViewModels.Pages;

public partial class SettingsViewModel : ObservableObject, INavigationAware, IViewable
{
    private readonly UpdateConfig updateConfig;
    private readonly KognitoConfig kognitoConfig;

    private bool _isInitialized = false;

    #region Properties

    [ObservableProperty]
    private string _fullAppVersion = string.Empty;

    [ObservableProperty]
    private string _appVersion = string.Empty;

    [ObservableProperty]
    private ApplicationTheme _currentTheme = ApplicationTheme.Unknown;

    [ObservableProperty]
    private string _clearedSize = string.Empty;

    [ObservableProperty]
    private string _appDataPath = string.Empty;

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
        updateConfig = ConfigurationFactory.GetConfig<UpdateConfig>();
        kognitoConfig = ConfigurationFactory.GetConfig<KognitoConfig>();

        AppDataPath = App.AppDataDirectory.FullName;
    }

    [RelayCommand]
    private static void OnCreateLogpack()
    {
        _ = CreateLogPack();
    }

    [RelayCommand]
    private void OnSaveUpdatesSettings()
    {
        ConfigurationFactory.SaveConfig(updateConfig);
    }

    [RelayCommand]
    private static void OnOpenAppData()
    {
        App.OpenDirectory(App.AppDataDirectory!.FullName);
    }

    [RelayCommand]
    private static async Task OnTransferConfigs()
    {
        try
        {
            ConfigurationFactory.TransferAppStartConfigurations();
        }
        catch (Exception ex)
        {
            _ = await new MessageBox()
            {
                Title = "Transfer Failed",
                Content = $"Failed to transfer configurations found in application startup directory.\n\n{ex.Message}"
            }.ShowDialogAsync();
        }

        _ = await new MessageBox()
        {
            Title = "Transfer Success",
            Content = "All valid configuration files found within the application startup directory were transfered to %APPDATA%\\configs successfully."
        }.ShowDialogAsync();
    }

    public void OnNavigatedTo()
    {
        if (!_isInitialized)
        {
            InitializeViewModel();
        }
    }

    public void OnNavigatedFrom()
    {
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
