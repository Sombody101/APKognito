﻿using APKognito.Configurations;
using APKognito.Services;
using APKognito.ViewModels.Pages;
using APKognito.ViewModels.Windows;
using APKognito.Views.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Threading;
using Wpf.Ui;

namespace APKognito;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    public static DirectoryInfo? AppData { get; private set; }

    // The.NET Generic Host provides dependency injection, configuration, logging, and other services.
    // https://docs.microsoft.com/dotnet/core/extensions/generic-host
    // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
    // https://docs.microsoft.com/dotnet/core/extensions/configuration
    // https://docs.microsoft.com/dotnet/core/extensions/logging
    private static readonly IHost _host = Host
        .CreateDefaultBuilder()
        .ConfigureAppConfiguration(c =>
        {
            _ = c.SetBasePath(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!);
        })
        .ConfigureServices((context, services) =>
        {
            _ = services.AddHostedService<ApplicationHostService>();

            // Page resolver service
            _ = services.AddSingleton<IPageService, PageService>();

            // Theme manipulation
            _ = services.AddSingleton<IThemeService, ThemeService>();

            // TaskBar manipulation
            _ = services.AddSingleton<ITaskBarService, TaskBarService>();

            // Service containing navigation, same as INavigationWindow... but without window
            _ = services.AddSingleton<INavigationService, NavigationService>();

            // Main window with navigation
            _ = services.AddSingleton<INavigationWindow, MainWindow>()
                .AddSingleton<MainWindowViewModel>();

            // Exception window model
            _ = services.AddSingleton<ExceptionWindowViewModel>();

            // Configuration factory
            _ = services.AddSingleton<ConfigurationFactory>();

            // Load all pages (any class that implements IViewable)
            IEnumerable<Type> types = typeof(App).Assembly.GetTypes()
                .Where(t => t != typeof(IViewable) && typeof(IViewable).IsAssignableFrom(t));

            foreach (Type? type in types)
            {
                _ = services.AddSingleton(type);
            }

        }).Build();

    /// <summary>
    /// Gets registered service.
    /// </summary>
    /// <typeparam name="T">Type of the service to get.</typeparam>
    /// <returns>Instance of the service or <see langword="null"/>.</returns>
    public static T? GetService<T>()
        where T : class
    {
        return _host.Services.GetService(typeof(T)) as T;
    }

    /// <summary>
    /// Occurs when the application is loading.
    /// </summary>
    private void OnStartup(object sender, StartupEventArgs e)
    {
        AppData = Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), nameof(APKognito)));

        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            ExceptionWindow.CreateNewExceptionWindow(e.Exception, _host, "AppMain [src: TaskScheduler]");
        };

        Dispatcher.UnhandledException += (sender, e) =>
        {
            ExceptionWindow.CreateNewExceptionWindow(e.Exception, _host, "AppMain [src: Default Dispatcher]");
        };

        _host.Start();
    }

    /// <summary>
    /// Occurs when the application is closing.
    /// </summary>
    private async void OnExit(object sender, ExitEventArgs e)
    {
        // Likely won't be rendered, but slow PCs might see it ¯\_(ツ)_/¯
        HomeViewModel.Instance?.Log("Saving settings...");
        _host.Services.GetService<ConfigurationFactory>()?.SaveAllConfigs();

        await _host.StopAsync();

        _host.Dispose();
    }

    public static void OpenHyperlink(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        _ = Process.Start(new ProcessStartInfo()
        {
            FileName = e.Uri.ToString(),

            // Starts it in the default browser. Otherwise, it will look for a file using the URL as a file path
            UseShellExecute = true
        });

        // If not handled, the page is rendered in the WPF page... (or at least attempted, because the CSS doesn't load lol)
        e.Handled = true;
    }
}
