using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace APKognito.Views.Pages;

/// <summary>
/// Interaction logic for AdvancedRenameConfigurationPage.xaml
/// </summary>
public partial class AdvancedRenameConfigurationPage : INavigableView<AdvancedRenameConfigurationViewModel>, IViewable
{
    public AdvancedRenameConfigurationViewModel ViewModel { get; }

    public AdvancedRenameConfigurationPage(AdvancedRenameConfigurationViewModel viewModel)
    {
        DataContext = this;
        ViewModel = viewModel;

        InitializeComponent();
    }

    public AdvancedRenameConfigurationPage()
    {
        // For designer
    }
}
