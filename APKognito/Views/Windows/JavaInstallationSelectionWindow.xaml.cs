using System.ComponentModel;
using APKognito.Base;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Models;
using APKognito.Utilities;
using APKognito.ViewModels.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace APKognito.Views.Windows;

/// <summary>
/// Interaction logic for JavaInstallationSelectionWindow.xaml
/// </summary>
public partial class JavaInstallationSelectionWindow : IDialogResult<JavaDownloadInfo>
{
    public JavaInstallationSelectionViewModel ViewModel { get; set; }

    public new JavaDownloadInfo? DialogResult { get; private set; }

#if DEBUG
    public JavaInstallationSelectionWindow()
    {
        // For designer
    }
#endif

    public JavaInstallationSelectionWindow(ConfigurationFactory configFactory, JavaInstallationSelectionViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        UserThemeConfig themeManager = configFactory.GetConfig<UserThemeConfig>();
        ApplicationThemeManager.Apply(themeManager.AppTheme, WindowBackdropType.None, false);

        if (themeManager.UseSystemAccent)
        {
            ApplicationAccentColorManager.ApplySystemAccent();
            SystemThemeWatcher.Watch(this);
        }

        InitializeComponent();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        ViewModel.LoadJdkVersionsCommandCommand.Cancel();

        base.OnClosing(e);
    }

    [CalledByGenerated]
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    [CalledByGenerated]
    private async void ContinueButton_ClickAsync(object sender, RoutedEventArgs e)
    {

        if (ViewModel.SelectedInstallation is not null)
        {
            DialogResult = ViewModel.SelectedInstallation with
            {
                InstallDirectory = ViewModel.CustomInstallPath
            };
        }

        Close();
    }
}
