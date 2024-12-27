using APKognito.ViewModels.Windows;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace APKognito.Views.Windows;

public partial class MainWindow : INavigationWindow
{
    public MainWindowViewModel ViewModel { get; }

    public MainWindow(
        MainWindowViewModel viewModel,
        ISnackbarService snackbarService,
        IPageService pageService,
        INavigationService navigationService
    )
    {
        ViewModel = viewModel;
        DataContext = this;

        SystemThemeWatcher.Watch(this);
        ApplicationAccentColorManager.ApplySystemAccent();

        InitializeComponent();
        SetPageService(pageService);
        snackbarService.SetSnackbarPresenter(SnackbarPresenter);

        navigationService.SetNavigationControl(RootNavigation);

        if (MainWindowViewModel.LaunchedAsAdministrator)
        {
            Loaded += async (sender, e) =>
            {
                // Give the window roughly a millisecond to render
                await Task.Delay(1);

                MessageBoxResult result = await new MessageBox()
                {
                    Title = "Launched as Admin!",
                    Content = "It's not recommended to launch an application as admin, especially one that interacts with your drive(s)! " +
                        "Continue only if you know what you're doing and are okay with the risk!",
                    PrimaryButtonText = "Exit",
                    CloseButtonText = "Continue anyway",
                    CloseButtonAppearance = ControlAppearance.Caution,
                }.ShowDialogAsync();

                if (result is MessageBoxResult.Primary)
                {
                    App.Current.Shutdown();
                }
            };
        }
    }

    #region INavigationWindow methods

    public INavigationView GetNavigation()
    {
        return RootNavigation;
    }

    public bool Navigate(Type pageType)
    {
        return RootNavigation.Navigate(pageType);
    }

    public void SetPageService(IPageService pageService)
    {
        RootNavigation.SetPageService(pageService);
    }

    public void ShowWindow()
    {
        Show();
    }

    public void CloseWindow()
    {
        Close();
    }

    #endregion INavigationWindow methods

    /// <summary>
    /// Raises the closed event.
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // Make sure that closing this window will begin the process of closing the application.
        Application.Current.Shutdown();
    }

    INavigationView INavigationWindow.GetNavigation()
    {
        throw new NotImplementedException();
    }

    public void SetServiceProvider(IServiceProvider serviceProvider)
    {
        throw new NotImplementedException();
    }
}