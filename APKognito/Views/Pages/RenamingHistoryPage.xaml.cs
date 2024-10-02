using APKognito.ViewModels.Pages;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace APKognito.Views.Pages;

/// <summary>
/// Interaction logic for RenamingHistoryPage.xaml
/// </summary>
public partial class RenamingHistoryPage : INavigableView<RenamingHistoryViewModel>, IViewable
{
    public RenamingHistoryViewModel ViewModel { get; }

    public RenamingHistoryPage(RenamingHistoryViewModel _viewModel)
    {
        DataContext = ViewModel = _viewModel;

        InitializeComponent();

        Loaded += (sender, e) =>
        {
            _ = Dispatcher.BeginInvoke(new Action(() => _ = _viewModel.RefreshRenameSessions()), DispatcherPriority.ContextIdle, null);
        };
    }
}
