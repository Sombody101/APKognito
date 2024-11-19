using APKognito.Configurations;
using APKognito.Services;
using APKognito.Utilities;
using APKognito.ViewModels.Pages;
using APKognito.ViewModels.Windows;
using APKognito.Views.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.Appearance;

namespace APKognito;

#pragma warning disable S2325 // Methods and properties that don't access instance data should be static
#pragma warning disable S6605 // Collection-specific "Exists" method should be used instead of the "Any" extension

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    private const string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.6446.71 Safari/537.36";
    private static HttpClient? _sharedHttpClient;

    public static DirectoryInfo AppData { get; } = Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), nameof(APKognito)));

    public static string GetVersion()
    {
        return $"{(IsDebugRelease ? 'd' : 'v')}{Assembly.GetExecutingAssembly().GetName().Version}";
    }

    public static bool IsDebugRelease =>
#if DEBUG
            true;
#else
            false;
#endif

    /// <summary>
    /// An <see cref="HttpClient"/> instance that is shared throughout the application.
    /// </summary>
    public static HttpClient SharedHttpClient
    {
        get
        {
            // Is only created when needed, stays open in the app after that (to save sockets)
            if (_sharedHttpClient is null)
            {
                _sharedHttpClient = new();
                _sharedHttpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
            }

            return _sharedHttpClient;
        }
    }

    // The.NET Generic Host provides dependency injection, configuration, logging, and other services.
    // https://docs.microsoft.com/dotnet/core/extensions/generic-host
    // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
    // https://docs.microsoft.com/dotnet/core/extensions/configuration
    // https://docs.microsoft.com/dotnet/core/extensions/logging
    private static readonly IHost _host = Host
        .CreateDefaultBuilder()
        .ConfigureAppConfiguration(c =>
        {
            _ = c.SetBasePath(AppContext.BaseDirectory);
        })
        .ConfigureServices((__, services) =>
        {
            _ = services.AddHostedService<ApplicationHostService>();

            _ = services.AddSingleton<ISnackbarService, SnackbarService>();

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

            // Auto update service
            _ = services.AddHostedService<AutoUpdaterService>();

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
        FileLogger.Log($"App start. v{Assembly.GetExecutingAssembly().GetName().Version}, {(IsDebugRelease ? "Debug" : "Release")}");

        TaskScheduler.UnobservedTaskException += (sender, e) => _ = ExceptionWindow.CreateNewExceptionWindow(e.Exception, _host, "AppMain [src: TaskScheduler]");
        Dispatcher.UnhandledException += (sender, e) => _ = ExceptionWindow.CreateNewExceptionWindow(e.Exception, _host, "AppMain [src: Default Dispatcher]");

        _host.Start();

        ApplicationAccentColorManager.ApplySystemAccent();
    }

    /// <summary>
    /// Occurs when the application is closing.
    /// </summary>
    private async void OnExit(object sender, ExitEventArgs e)
    {
        // Likely won't be rendered, but slow PCs might see it ¯\_(ツ)_/¯
        HomeViewModel.Instance?.Log("Saving all settings...");
        ConfigurationFactory.SaveAllConfigs();

        await _host.StopAsync();

        _host.Dispose();
        FileLogger.Log("App exit.");
    }

    /// <summary>
    /// Opens a hyperlink in the users default browser.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
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

    /// <summary>
    /// Opens a directory in File Explorer. If the directory does not exist and <paramref name="notifyMissing"/> is 
    /// <see langword="true"/>, then a <see cref="MessageBox"/> is presented with a "not found" message.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="notifyMissing"></param>
    public static void OpenDirectory(string directory, bool notifyMissing = true)
    {
        if (!Directory.Exists(directory))
        {
            if (notifyMissing)
            {
                _ = new MessageBox()
                {
                    Title = "Failed to open directory",
                    Content = $"Failed to open directory as it does not exist.\n\n{directory}",
                }.ShowDialogAsync();
            }

            return;
        }

        try
        {
            _ = Process.Start("explorer", directory);
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
        }
    }
}