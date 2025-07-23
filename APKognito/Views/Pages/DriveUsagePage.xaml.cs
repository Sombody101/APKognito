using APKognito.Models;
using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

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
    }

    private void FolderList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        ViewModel.TotalSelectedSpace = FolderList.SelectedItems
            .Cast<FootprintInfo>()
            .Select(f => f.FolderSizeBytes)
            .Aggregate((a, c) => a + c);
    }
}
