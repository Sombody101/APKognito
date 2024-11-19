using APKognito.ViewModels.Pages;
using Wpf.Ui.Controls;

namespace APKognito.Views.Pages;

/// <summary>
/// Interaction logic for AdbConfigurationPage.xaml
/// </summary>
public partial class AdbConfigurationPage : INavigableView<AdbConfigurationViewModel>, IViewable
{
    public AdbConfigurationViewModel ViewModel { get; }

    public AdbConfigurationPage(AdbConfigurationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = this;
        ViewModel = viewModel;

        Loaded += async (sender, e) => await viewModel.RefreshDevicesList();
    }

    private void ComboBox_DropDownOpened(object sender, EventArgs e)
    {
        _ = Dispatcher.Invoke(ViewModel.RefreshDevicesList);
    }
}
