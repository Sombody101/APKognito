using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Windows;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;

namespace APKognito.Views.Windows;

/// <summary>
/// Interaction logic for SetupWizard.xaml
/// </summary>
public partial class SetupWizard : INavigableView<SetupWizardViewModel>, IViewable
{
    private readonly INavigationService _navigationService;

    public SetupWizardViewModel ViewModel { get; }

    public SetupWizard()
    {
        // For designer
    }

    public SetupWizard(
        SetupWizardViewModel wizardVm,
        INavigationService navigationService
    )
    {
        ViewModel = wizardVm;
        DataContext = this;

        InitializeComponent();

        SystemThemeWatcher.Watch(this);
        ApplicationAccentColorManager.ApplySystemAccent();

        _navigationService = navigationService;
        //navigationService.SetNavigationControl(SetupFrame);

    }
}
