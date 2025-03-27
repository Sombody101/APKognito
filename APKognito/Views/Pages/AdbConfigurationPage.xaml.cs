using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace APKognito.Views.Pages;

/// <summary>
/// Interaction logic for AdbConfigurationPage.xaml
/// </summary>
public partial class AdbConfigurationPage : INavigableView<AdbConfigurationViewModel>, IViewable
{
    public AdbConfigurationViewModel ViewModel { get; }

    public AdbConfigurationPage()
    {
        // For designer
    }

    public AdbConfigurationPage(AdbConfigurationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = ViewModel = viewModel;
    }
}
