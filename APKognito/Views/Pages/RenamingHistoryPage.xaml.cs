using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Pages;
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

        Application.Current.Dispatcher.Invoke((Action)async delegate
        {
            await _viewModel.RefreshRenameSessions();
        });
    }
}