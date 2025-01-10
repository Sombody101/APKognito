using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.ViewModels.Pages;
using Wpf.Ui.Controls;

using TreeViewItem = Wpf.Ui.Controls.TreeViewItem;

namespace APKognito.Views.Pages;

/// <summary>
/// Interaction logic for FileExplorerPage.xaml
/// </summary>
public partial class FileExplorerPage : INavigableView<FileExplorerViewModel>, IViewable, System.Windows.Markup.IStyleConnector
{
    public FileExplorerViewModel ViewModel { get; }

    public FileExplorerPage(FileExplorerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = ViewModel = viewModel;

        Loaded += (sender, e) =>
        {
            viewModel.SetAndInitializePageSize(this);

            if (ConfigurationFactory.TryGetConfig<AdbConfig>(out var adbConfig) && adbConfig!.CurrentDeviceId is not null)
            {
                // A default device exists, so start adding items early
                _ = ViewModel.GetFolders(null);
            }
        };
    }

    bool propagationDebouce = false;
    private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (propagationDebouce)
        {
            return;
        }

        propagationDebouce = true;

        _ = ViewModel.GetFolders(sender as TreeViewItem);
        propagationDebouce = false;
    }

    private void TreeViewItem_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        Dispatcher.Invoke(() => ViewModel.SelectFolder((TreeViewItem)sender));
    }
}
