using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Pages;
using System.Windows.Data;
using Wpf.Ui.Abstractions.Controls;

using TextBox = System.Windows.Controls.TextBox;

namespace APKognito.Views.Pages;

/// <summary>
/// Interaction logic for UninstallerPage.xaml
/// </summary>
public partial class PackageManagerPage : INavigableView<PackageManagerViewModel>, IViewable
{
    public PackageManagerViewModel ViewModel { get; }

    public PackageManagerPage(PackageManagerViewModel viewModel)
    {
        DataContext = ViewModel = viewModel;

        InitializeComponent();

        Loaded += async (sender, e) =>
        {
            await viewModel.UpdatePackageListAsync(true);
        };
    }

    private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        TextBox tBox = (TextBox)sender;
        DependencyProperty prop = TextBox.TextProperty;

        BindingExpression binding = BindingOperations.GetBindingExpression(tBox, prop);
        binding?.UpdateSource();
    }
}
