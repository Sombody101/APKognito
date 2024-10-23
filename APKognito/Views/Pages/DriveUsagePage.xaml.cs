using APKognito.Models;
using APKognito.ViewModels.Pages;
using Wpf.Ui.Controls;

namespace APKognito.Views.Pages;

/// <summary>
/// Interaction logic for DriveUsagePage.xaml
/// </summary>
public partial class DriveUsagePage : INavigableView<DriveUsageViewModel>, IViewable
{
    public DriveUsageViewModel ViewModel { get; }

    public DriveUsagePage(DriveUsageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = ViewModel = viewModel;

        Loaded += (sender, e) => Dispatcher.Invoke(async () => await viewModel.StartSearch());
    }

    private void FolderList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        ViewModel.TotalSelectedSpace = FolderList.SelectedItems.Cast<FootprintInfo>().Sum(item => item.FolderSizeBytes);
    }
}