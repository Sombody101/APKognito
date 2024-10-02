using APKognito.ViewModels.Pages;
using System.Windows.Threading;
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

        Loaded += (sender, e) =>
        {
            _ = Dispatcher.BeginInvoke(new Action(() => viewModel.StartSearchCommand.Execute(null)), DispatcherPriority.ContextIdle, null);
        };
    }
}
