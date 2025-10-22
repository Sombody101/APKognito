using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Windows;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace APKognito.Views.Windows;

/// <summary>
/// Interaction logic for SetupWizard.xaml
/// </summary>
public partial class SetupWizardWindow : INavigationWindow, INavigableView<SetupWizardViewModel>, IViewable
{
    private readonly INavigationService _navigationService;

    public SetupWizardViewModel ViewModel { get; }

    public SetupWizardWindow()
    {
        // For designer
        ViewModel = null!;
        _navigationService = null!;
    }

    public SetupWizardWindow(
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
    }

    public INavigationView GetNavigation()
    {
        return RootNavigation;
    }

    public bool Navigate(Type pageType)
    {
        return RootNavigation.Navigate(pageType);
    }

    public void SetPageService(INavigationViewPageProvider navigationViewPageProvider)
    {
        RootNavigation.SetPageProviderService(navigationViewPageProvider);
    }

    public void ShowWindow()
    {
        Show();
    }

    public void CloseWindow()
    {
        Close();
    }

    public void SetServiceProvider(IServiceProvider serviceProvider)
    {
        throw new NotImplementedException();
    }
}
