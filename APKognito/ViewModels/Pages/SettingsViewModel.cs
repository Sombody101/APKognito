using APKognito.Utilities;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

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

    public void OnNavigatedFrom()
    { }

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

    [RelayCommand]
    private static void OnCreateLogpack()
    {
        _ = SettingsViewModel.CreateLogPack();
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