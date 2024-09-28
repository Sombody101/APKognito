using System.IO;
using System.Reflection;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace APKognito.ViewModels.Pages;

public partial class SettingsViewModel : ObservableObject, INavigationAware, IViewable
{
    private bool _isInitialized = false;

    [ObservableProperty]
    private string _appVersion = string.Empty;

    [ObservableProperty]
    private string _appDescription = string.Empty;

    [ObservableProperty]
    private ApplicationTheme _currentTheme = ApplicationTheme.Unknown;

    [ObservableProperty]
    private string _clearedSize = string.Empty;

    public void OnNavigatedTo()
    {
        if (!_isInitialized)
        {
            InitializeViewModel();
        }
    }

    public void OnNavigatedFrom() { }

    private void InitializeViewModel()
    {
        CurrentTheme = ApplicationThemeManager.GetAppTheme();

        AppDescription = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? "No description found.";

        string appName = Assembly.GetExecutingAssembly().GetName().Name ?? string.Empty;
        string appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
        AppVersion = $"{appName} - {appVersion}";

        _isInitialized = true;
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
    private void ClearTempFiles()
    {
        string[] directories = Directory.GetDirectories(Path.GetTempPath(), "APKognito-*");

        long calculatedSize = 0;

        foreach (string directory in directories)
        {
            calculatedSize += DirSize(new DirectoryInfo(directory));
            Directory.Delete(directory, true);
        }

        ClearedSize = $"Deleted {directories.Length} temp folders. ({calculatedSize / 1024 / 1024} MB)";
    }

    private static long DirSize(DirectoryInfo d)
    {
        long size = 0;
        // Add file sizes.
        FileInfo[] fis = d.GetFiles();
        foreach (FileInfo fi in fis)
        {
            size += fi.Length;
        }
        // Add subdirectory sizes.
        DirectoryInfo[] dis = d.GetDirectories();
        foreach (DirectoryInfo di in dis)
        {
            size += DirSize(di);
        }
        return size;
    }
}
