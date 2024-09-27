using APKognito.ViewModels.Pages;
using Wpf.Ui.Controls;

namespace APKognito.Views.Pages
{
    public partial class SettingsPage : INavigableView<SettingsViewModel>, IViewable
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage(SettingsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }
    }
}
