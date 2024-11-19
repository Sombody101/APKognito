using APKognito.Views.Pages;
using System.Collections.ObjectModel;
using Wpf.Ui.Controls;

namespace APKognito.ViewModels.Windows;

public partial class MainWindowViewModel : ObservableObject, IViewable
{
    [ObservableProperty]
    private string _applicationTitle = "APKognito";

    [ObservableProperty]
    private ObservableCollection<object> _menuItems =
    [
        new NavigationViewItem()
        {
            Content = "Rename APK",
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
#if DEBUG
        new NavigationViewItem()
        {
            Content = "ADB",
            Icon = new SymbolIcon { Symbol = SymbolRegular.Code16 },
            TargetPageType = typeof(AdbConsolePage),
            MenuItemsSource = new NavigationViewItem[] {
                new("ADB Configuration", typeof(AdbConfigurationPage)),
                new("Console", typeof(AdbConsolePage)),
                // new("Quick Commands", typeof(AdbConsolePage)),
                new("File Explorer", typeof(FileExplorerPage)),
                new("File Uploader", typeof(FileUploaderPage)),
            },
        },
#endif
    ];

    [ObservableProperty]
    private ObservableCollection<object> _footerMenuItems =
    [
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
}