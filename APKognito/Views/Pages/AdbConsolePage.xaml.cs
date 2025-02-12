using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Pages;
using System.Windows.Data;
using System.Windows.Interop;
using Wpf.Ui.Controls;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace APKognito.Views.Pages;

/// <summary>
/// Interaction logic for AdbConsole.xaml
/// </summary>
public partial class AdbConsolePage : INavigableView<AdbConsoleViewModel>, IViewable
{
    public AdbConsoleViewModel ViewModel { get; }

    public AdbConsolePage(AdbConsoleViewModel viewModel)
    {
        DataContext = ViewModel = viewModel;

        InitializeComponent();

        viewModel.AntiMvvm_SetRichTextbox(CommandOutputBox);
        viewModel.WriteGenericLogLine("Enter `:help' for a list of APKognito commands, use `help' to get ADB commands.");
    }

    private void TextBox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        BindingOperations.GetBindingExpression((TextBox)sender, TextBox.TextProperty)?.UpdateSource();
    }
}
