using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace APKognito.Views.Pages;

/// <summary>
/// Interaction logic for RenameConfigurationPage.xaml
/// </summary>
public sealed partial class RenameConfigurationPage : INavigableView<RenameConfigurationViewModel>, IViewable
{
    public RenameConfigurationViewModel ViewModel { get; }

    public RenameConfigurationPage()
    {
        // For designer
    }

    public RenameConfigurationPage(RenameConfigurationViewModel viewModel)
    {
        DataContext = this;
        ViewModel = viewModel;

        InitializeComponent();
    }

    [CalledByGenerated]
    private void TextBox_KeyUp(object sender, KeyEventArgs e)
    {
        App.ForwardKeystrokeToBinding(sender);
    }

    [CalledByGenerated]
    private async void CheckBox_CheckedAsync(object sender, RoutedEventArgs e)
    {
        await RenameConfigurationViewModel.OnRenameCopyCheckedAsync();
    }
}
