using APKognito.Configurations;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using APKognito.Views.Pages;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Principal;
using Wpf.Ui;
using Wpf.Ui.Controls;
using APKognito.Helpers;
using APKognito.Controls;
using APKognito.Configurations.ConfigModels;



#if DEBUG
using APKognito.Views.Pages.Debugging;
#endif

namespace APKognito.ViewModels.Windows;

public partial class MainWindowViewModel : LoggableObservableObject
{
    private readonly ConfigurationFactory _configFactory;

    #region Properties

    [ObservableProperty]
    public partial string ApplicationTitle { get; set; } = $"APKognito{(LaunchedAsAdministrator ? " [ADMIN]" : string.Empty)} {GetBuildTypeString()}";

    [ObservableProperty]
    public partial ObservableCollection<object> MenuItems { get; set; } =
    [
        new NavigationViewItem()
        {
            Content = "Package Renamer",
            Icon = new SymbolIcon { Symbol = SymbolRegular.Box16 },
            TargetPageType = typeof(HomePage),
            MenuItemsSource = new NavigationViewItem[] {
                new("Rename Settings", SymbolRegular.BuildingLighthouse20, typeof(RenameConfigurationPage)),
            },
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
                new("ADB Configuration", SymbolRegular.PlaySettings20, typeof(AdbConfigurationPage)),
                new("Console", SymbolRegular.WindowConsole20, typeof(AdbConsolePage)),
                new("File Explorer", SymbolRegular.DocumentSearch20, typeof(FileExplorerPage)),
                new("File Uploader", SymbolRegular.ArchiveArrowBack20, typeof(FileUploaderPage)),
                new("Package Manager", SymbolRegular.DeveloperBoard20, typeof(PackageManagerPage)),
            },
        },
    ];

    [ObservableProperty]
    public partial ObservableCollection<object> FooterMenuItems { get; set; } =
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
    public partial ObservableCollection<MenuItem> TrayMenuItems { get; set; } =
    [
        new MenuItem { Header = "Rename APK", Tag = "tray_home" },
        new MenuItem { Header = "Close", Tag = "tray_close" }
    ];

    #endregion Properties

    public MainWindowViewModel()
    {
        // For designer
        _configFactory = null!;
    }

    public MainWindowViewModel(ISnackbarService snack, ConfigurationFactory configFactory)
    {
        SetSnackbarProvider(snack);
        _configFactory = configFactory;
        AddAdbDeviceTray();
    }

    public MainWindowViewModel(ObservableCollection<object> footerMenuItems)
    {
        FooterMenuItems = footerMenuItems;
        _configFactory = null!;
    }

    #region Commands

    [RelayCommand]
    private void OnSaveAllConfigs()
    {
        try
        {
            _configFactory.SaveAllConfigs();
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
    [SuppressMessage("Critical Code Smell", "S1215:GC.Collect should not be called", Justification = "I don't care.")]
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

        SnackSuccess("GC Success!", $"Cleaned {GBConverter.FormatSizeFromBytes(memoryUsage - memoryAfterClean)}");

        _cleanupDebounce = false;
    }

    [RelayCommand]
    private static void OnSimulateCrash()
    {
#if DEBUG
        throw new DebugOnlyException();
#else
        FileLogger.Log($"Artificial crash attempted on {App.Version.VersionIdentifier} build.");
#endif
    }

    #endregion Commands

    public void AddAdbDeviceTray()
    {
        if (FooterMenuItems.Any(i => ((NavigationViewItem)i).Content is AndroidDeviceInfo))
        {
            return;
        }

        AddAdbDeviceTrayInternal(false);
    }

    private void AddAdbDeviceTrayInternal(bool initRun)
    {
        AdbConfig adbConfig = _configFactory.GetConfig<AdbConfig>();
        if (!adbConfig.AdbWorks())
        {
            return;
        }

        var newItem = new NavigationViewItem()
        {
            Content = new AndroidDeviceInfo(InfoRenderType.SideMenu),
            TargetPageType = typeof(AdbConfigurationPage),
        };

        if (initRun)
        {
            FooterMenuItems.Insert(0, newItem);
        }
        else
        {
            FooterMenuItems = [newItem, .. FooterMenuItems];
        }
    }

    public static readonly bool LaunchedAsAdministrator = new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);

    private static string GetBuildTypeString()
    {
#if RELEASE
        return string.Empty;
#else
        return $"[{App.Version.VersionIdentifier.ToUpper()}]";
#endif
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
