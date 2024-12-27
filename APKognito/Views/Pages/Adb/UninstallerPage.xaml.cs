using APKognito.ViewModels.Pages;
using System.Windows.Data;
using Wpf.Ui.Controls;

using TextBox = System.Windows.Controls.TextBox;

namespace APKognito.Views.Pages;

/// <summary>
/// Interaction logic for UninstallerPage.xaml
/// </summary>
public partial class UninstallerPage : INavigableView<UninstallerViewModel>, IViewable
{
    public UninstallerViewModel ViewModel { get; }

    public UninstallerPage(UninstallerViewModel viewModel)
    {
        DataContext = ViewModel = viewModel;

        InitializeComponent();

        Loaded += async (sender, e) =>
        {
            ViewModel.SetPage(this);
            await viewModel.UpdatePackageListCommand.ExecuteAsync(null);
        };
    }

    private void TextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        TextBox tBox = (TextBox)sender;
        DependencyProperty prop = TextBox.TextProperty;

        BindingExpression binding = BindingOperations.GetBindingExpression(tBox, prop);
        binding?.UpdateSource();
    }
}
