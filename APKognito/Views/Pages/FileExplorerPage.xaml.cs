using APKognito.Models;
using APKognito.ViewModels.Pages;
using Wpf.Ui.Controls;
using ListViewItem = Wpf.Ui.Controls.ListViewItem;

namespace APKognito.Views.Pages;

/// <summary>
/// Interaction logic for FileExplorerPage.xaml
/// </summary>
public partial class FileExplorerPage : INavigableView<FileExplorerViewModel>, IViewable
{
    public FileExplorerViewModel ViewModel { get; }

    public FileExplorerPage(FileExplorerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = ViewModel = viewModel;

        Loaded += async (sender, e) =>
        {
            await viewModel.NavigateToDirectoryCommand.ExecuteAsync(AdbFolderInfo.RootFolder);
        };
    }

    private async void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        AdbFolderInfo info;

        if (sender is not ListViewItem item)
        {
            return;
        }

        info = (AdbFolderInfo)item.Content;
        if (info.ItemType is not (AdbFolderType.Directory or AdbFolderType.SymbolicLink))
        {
            return;
        }

        await ViewModel.NavigateToDirectoryCommand.ExecuteAsync(info);
    }

    private void ListViewItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {

    }
}