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

        Task.Run(async () =>
        {
            await viewModel.StartSearch();
        });
    }
}