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
            TargetPageType = typeof(Views.Pages.HomePage)
        },
        new NavigationViewItem()
        {
            Content = "Drive Footprint",
            Icon = new SymbolIcon { Symbol = SymbolRegular.HardDrive16 },
            TargetPageType = typeof(Views.Pages.DriveUsagePage)
        },
        new NavigationViewItem()
        {
            Content = "Rename History",
            Icon = new SymbolIcon { Symbol = SymbolRegular.History16 },
            TargetPageType = typeof(Views.Pages.RenamingHistoryPage)
        },
    ];

    [ObservableProperty]
    private ObservableCollection<object> _footerMenuItems =
    [
        new NavigationViewItem()
        {
            Content = "Settings",
            Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
            TargetPageType = typeof(Views.Pages.SettingsPage)
        }
    ];

    [ObservableProperty]
    private ObservableCollection<MenuItem> _trayMenuItems =
    [
        new MenuItem { Header = "Rename APK", Tag = "main_menu" },
    ];
}