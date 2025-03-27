using APKognito.Configurations;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using APKognito.Views.Pages;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Principal;
using Wpf.Ui;
using Wpf.Ui.Controls;

#if DEBUG
using APKognito.Views.Pages.Debugging;
#endif

using MenuItem = System.Windows.Controls.MenuItem;

namespace APKognito.ViewModels.Windows;

public partial class MainWindowViewModel : LoggableObservableObject
{
    #region Properties

    [ObservableProperty]
    private string _applicationTitle = $"APKognito{(LaunchedAsAdministrator ? " [ADMIN]" : string.Empty)} {GetBuildTypeString()}";

    [ObservableProperty]
    private ObservableCollection<object> _menuItems =
    [
        new NavigationViewItem()
        {
            Content = "Package Renamer",
            Icon = new SymbolIcon { Symbol = SymbolRegular.Box16 },
            TargetPageType = typeof(HomePage)
        },
        new NavigationViewItem()
        {
            Content = "Drive Footprint",
            Icon = new SymbolIcon { Symbol = SymbolRegular.HardDrive16 },
            TargetPageType = typeof(DriveUsagePage)
        },
        new NavigationViewItem()
        {
            Content = "Rename History",
            Icon = new SymbolIcon { Symbol = SymbolRegular.History16 },
            TargetPageType = typeof(RenamingHistoryPage)
        },
        new NavigationViewItem()
        {
            Content = "ADB",
            Icon = new SymbolIcon { Symbol = SymbolRegular.Code16 },
            TargetPageType = typeof(AdbConsolePage),
            MenuItemsSource = new NavigationViewItem[] {
                new("ADB Configuration",    typeof(AdbConfigurationPage)),
                new("Console",              typeof(AdbConsolePage)),
                new("File Explorer",        typeof(FileExplorerPage)),
                new("File Uploader",        typeof(FileUploaderPage)),
                new("Package Manager",      typeof(PackageManagerPage)),
            },
        },
    ];

    [ObservableProperty]
    private ObservableCollection<object> _footerMenuItems =
    [
#if DEBUG
        new NavigationViewItem()
        {
            Content = "Log Viewer",
            Icon = new SymbolIcon { Symbol = SymbolRegular.ContentViewGallery28 },
            TargetPageType = typeof(LogViewerPage)
        },
#endif
        new NavigationViewItem()
        {
            Content = "Settings",
            Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
            TargetPageType = typeof(SettingsPage)
        },
    ];

    [ObservableProperty]
    private ObservableCollection<MenuItem> _trayMenuItems =
    [
        new MenuItem { Header = "Rename APK", Tag = "tray_home" },
        new MenuItem { Header = "Close", Tag = "tray_close" },
    ];

    #endregion Properties

    public MainWindowViewModel()
    {
        // For designer
    }

    public MainWindowViewModel(ISnackbarService snack)
    {
        SetSnackbarProvider(snack);
    }

    #region Commands

    [RelayCommand]
    private void OnSaveAllConfigs()
    {
        try
        {
            ConfigurationFactory.Instance.SaveAllConfigs();
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            SnackError("Error while saving configs!", ex.Message);
            return;
        }

        SnackSuccess("Configs saved!", "All configurations have been saved to file!");
    }

    private bool _cleanupDebounce = false;

    [RelayCommand]
    [SuppressMessage("Critical Code Smell", "S1215:GC.Collect\" should not be called", Justification = "I don't care.")]
    private async Task OnForceGarbageCollectionAsync()
    {
        if (_cleanupDebounce)
        {
            return;
        }

        _cleanupDebounce = true;

        long memoryUsage = GetMemSize();

        try
        {
            GC.Collect();
            await Task.Delay(1);
            GC.WaitForPendingFinalizers();
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            SnackError("Failed to run GC!", ex.Message);
            return;
        }

        long memoryAfterClean = GetMemSize();

        SnackSuccess("GC Success!", $"Cleaned {(memoryUsage - memoryAfterClean) / 1024f / 1024f:n2} MB");

        _cleanupDebounce = false;
    }

    [RelayCommand]
    private static void OnSimulateCrash()
    {
#if DEBUG
        throw new DebugOnlyException();
#endif

        FileLogger.Log($"Artificial crash attempted on {App.Version.VersionIdentifier} build.");
    }

    #endregion Commands

    public static readonly bool LaunchedAsAdministrator =
        new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);

    [SuppressMessage("Major Bug", "S2583:Conditionally executed code should be reachable", Justification = "It's reachable when built for it.")]
    private static string GetBuildTypeString()
    {
        App.Version.VersionTypeValue type = App.Version.VersionType;

        return type is App.Version.VersionTypeValue.Release
            ? string.Empty
            : $"[{App.Version.VersionIdentifier.ToUpper()}]";
    }

    private static long GetMemSize()
    {
        Process proc = Process.GetCurrentProcess();

        PerformanceCounter PC = new()
        {
            CategoryName = "Process",
            CounterName = "Working Set - Private",
            InstanceName = proc.ProcessName
        };

        long memsize = Convert.ToInt64(PC.NextValue());

        PC.Close();
        PC.Dispose();

        return memsize;
    }
}