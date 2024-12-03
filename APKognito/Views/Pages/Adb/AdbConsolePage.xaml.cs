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

        Loaded += (sender, e) =>
        {
            Window window = Window.GetWindow(this);

            // Initialize the size
            UpdateSizeChanged(window, window.WindowState is WindowState.Maximized);

            window.SizeChanged += (sender, e) =>
            {
                if ((long)ViewModel.MaxHeight != (long)window.Height)
                {
                    UpdateSizeChanged(window);
                }
            };

            window.StateChanged += (sender, e) =>
            {
                if (window.WindowState == WindowState.Maximized)
                {
                    UpdateSizeChanged(window, true);

                    // Assign the size to the window as it doesn't update when maximized
                    window.Height = ViewModel.MaxHeight;
                    return;
                }

                UpdateSizeChanged(window);
            };
        };

        viewModel.AntiMvvm_SetRichTextbox(CommandOutputBox);
        viewModel.WriteGenericLogLine("Enter `:help' for a list of APKognito commands, use `help' to get ADB commands.");
    }

    private void UpdateSizeChanged(Window window, bool useScreenHeight = false)
    {
        double baseHeight = window.Height;

        if (useScreenHeight)
        {
            Screen screen = Screen.FromHandle(new WindowInteropHelper(window).Handle);
            baseHeight = screen.Bounds.Height;
        }

        ViewModel.MaxHeight = baseHeight - 150;
    }

    private void TextBox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        BindingOperations.GetBindingExpression((TextBox)sender, TextBox.TextProperty)?.UpdateSource();
    }

    private void ComboBox_DropDownOpened(object sender, EventArgs e)
    {
        Dispatcher.Invoke(ViewModel.RefreshDevicesList);
    }
}
