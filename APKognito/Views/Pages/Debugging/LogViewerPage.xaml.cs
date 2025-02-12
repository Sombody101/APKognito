using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Pages.Debugging;
using Wpf.Ui.Controls;
using ComboBox = System.Windows.Controls.ComboBox;

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

        viewModel.AntiMvvm_SetRichTextbox(LogView);
    }

    public LogViewerPage()
    {
        // For designer
        DataContext = this;
        ViewModel = new();
    }

    private async void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        string? path = ViewModel.LogpackPath ?? ((string)((ComboBox)sender).SelectedItem);

        if (path is null)
        {
            return;
        }

        ViewModel.LogpackPath = path;
        await ViewModel.OpenLogpack(path);
    }
}
