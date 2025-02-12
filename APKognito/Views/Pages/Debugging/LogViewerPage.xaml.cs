using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Pages.Debugging;
using Wpf.Ui.Controls;

namespace APKognito.Views.Pages.Debugging;

/// <summary>
/// Interaction logic for LogViewerPage.xaml
/// </summary>
public partial class LogViewerPage : INavigableView<LogViewerViewModel>, IViewable
{
    public LogViewerViewModel ViewModel { get; }

    public LogViewerPage(LogViewerViewModel viewModel)
    {
        DataContext = this;
        ViewModel = viewModel;

        InitializeComponent();
    }

    public LogViewerPage()
    {
        // For designer
        DataContext = this;
        ViewModel = new();
    }
}
