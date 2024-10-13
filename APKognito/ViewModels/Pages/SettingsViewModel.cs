using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Utilities;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace APKognito.ViewModels.Pages;

public partial class SettingsViewModel : ObservableObject, INavigationAware, IViewable
{
    private readonly ConfigurationFactory configFactory;
    private readonly UpdateConfig updateConfig;

    private bool _isInitialized = false;

    #region Properties

    [ObservableProperty]
    private string _appVersion = string.Empty;

    [ObservableProperty]
    private string _appDescription = string.Empty;

    [ObservableProperty]
    private ApplicationTheme _currentTheme = ApplicationTheme.Unknown;

    [ObservableProperty]
    private string _clearedSize = string.Empty;

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

    public SettingsViewModel(ConfigurationFactory factory)
    {
        configFactory = factory;
        updateConfig = factory.GetConfig<UpdateConfig>();
    }

    [RelayCommand]
    private void OnChangeTheme(string parameter)
    {
        switch (parameter)
        {
            case "theme_light":
                if (CurrentTheme == ApplicationTheme.Light)
                {
                    break;
                }

                ApplicationThemeManager.Apply(ApplicationTheme.Light);
                CurrentTheme = ApplicationTheme.Light;

                break;

            default:
                if (CurrentTheme == ApplicationTheme.Dark)
                {
                    break;
                }

                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                CurrentTheme = ApplicationTheme.Dark;

                break;
        }
    }

    [RelayCommand]
    private static void OnCreateLogpack()
    {
        _ = SettingsViewModel.CreateLogPack();
    }

    [RelayCommand]
    private void OnSaveUpdatesSettings()
    {
        configFactory.SaveConfig(updateConfig);
    }

    [RelayCommand]
    private void OnOpenAppData()
    {
        App.OpenDirectory(App.AppData!.FullName);
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

        AppDescription = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? "No description found.";

        string appName = Assembly.GetExecutingAssembly().GetName().Name ?? string.Empty;
        string appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
        AppVersion = $"{appName} - {appVersion}";

        _isInitialized = true;
    }

    public static string CreateLogPack()
    {
        try
        {
            string logpackPath = FileLogger.CreateLogpack();

            var result = new MessageBox()
            {
                Title = "Logpack Created",
                Content = $"A logpack has been created at:\n{logpackPath}",
                PrimaryButtonText = "Open"
            }.ShowDialogAsync().Result;

            if (result == MessageBoxResult.Primary)
            {
                _ = Process.Start("explorer", Path.GetDirectoryName(logpackPath)!);
            }

            return logpackPath;
        }
        catch (Exception ex)
        {
            new MessageBox()
            {
                Title = "Logpack Failed",
                Content = $"Failed to create logpack.\n\n{ex.Message}",
            }.ShowDialogAsync();
        }

        return string.Empty;
    }
}
