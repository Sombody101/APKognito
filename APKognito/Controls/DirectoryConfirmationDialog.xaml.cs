using APKognito.Controls.ViewModel;
using System.IO;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace APKognito.Controls;

/// <summary>
/// Interaction logic for DirectoryConfirmationDialog.xaml
/// </summary>
public partial class DirectoryConfirmationDialog : ContentDialog
{
    public DirectoryConfirmationViewModel ViewModel { get; set; }

    public DirectoryConfirmationDialog()
    {
        // For designer
    }

    public DirectoryConfirmationDialog(ContentPresenter? presenter)
        : this(new(), presenter)
    {
    }

    public DirectoryConfirmationDialog(DirectoryConfirmationViewModel viewModel, ContentPresenter? presenter)
        : base(presenter)
    {
        DataContext = this;
        ViewModel = viewModel;
        InitializeComponent();
    }

    protected override void OnButtonClick(ContentDialogButton button)
    {
        switch (button)
        {
            case ContentDialogButton.Close:
                base.OnButtonClick(button);
                break;

            case ContentDialogButton.Primary:
                {
                    char[] offending = Path.GetInvalidPathChars();
                    if (ViewModel.OutputDirectory.IndexOfAny(offending) == 0)
                    {
                        TextBlock.SetCurrentValue(VisibilityProperty, Visibility.Visible);
                        _ = OutputDirectoryField.Focus();
                        return;
                    }
                }
                break;
        }

        base.OnButtonClick(button);
    }
}
