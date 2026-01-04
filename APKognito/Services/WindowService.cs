using APKognito.Models;
using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Windows;
using APKognito.Views.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace APKognito.Services;

public interface IWindowService
{
    JavaDownloadInfo? PromptJavaInstallationWindow(IViewLogger logger);
}

public sealed class WindowService(IServiceProvider serviceProvider) : IWindowService
{
    public JavaDownloadInfo? PromptJavaInstallationWindow(IViewLogger logger)
    {
        JavaInstallationSelectionViewModel viewmodel = ActivatorUtilities.CreateInstance<JavaInstallationSelectionViewModel>(serviceProvider, logger);
        JavaInstallationSelectionWindow window = ActivatorUtilities.CreateInstance<JavaInstallationSelectionWindow>(serviceProvider, viewmodel);

        window.Owner = Application.Current.MainWindow;

        _ = window.ShowDialog();

        return window.DialogResult;
    }
}
