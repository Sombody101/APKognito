// #define NO_EXCEPTION_HANDLING

using APKognito.Configurations;
using APKognito.Services;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Pages;
using APKognito.ViewModels.Windows;
using APKognito.Views.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Data;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;

namespace APKognito;

#pragma warning disable S2325 // Methods and properties that don't access instance data should be static

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    public static DirectoryInfo AppDataDirectory { get; } = Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), nameof(APKognito)));

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

            _ = services.AddSingleton<ISnackbarService, SnackbarService>()
                .AddSingleton<INavigationViewPageProvider, PageService>()
                .AddSingleton<IThemeService, ThemeService>()
                .AddSingleton<ITaskBarService, TaskBarService>()
                .AddSingleton<INavigationService, NavigationService>()
                .AddSingleton<IContentDialogService, ContentDialogService>()
                .AddSingleton<INavigationWindow, MainWindow>()
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
        FileLogger.Log($"App start. {Version.GetFullVersion()}, {Version.VersionIdentifier}");

#if !NO_EXCEPTION_HANDLING || RELEASE
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                _ = ExceptionWindow.CreateNewExceptionWindow(e.Exception, _host, "AppMain [src: TaskScheduler]");
            });
        };

        Dispatcher.UnhandledException += (sender, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                _ = ExceptionWindow.CreateNewExceptionWindow(e.Exception, _host, "AppMain [src: Default Dispatcher]");
            });
        };
#endif

        _host.Start();

        ApplicationAccentColorManager.ApplySystemAccent();
    }

    /// <summary>
    /// Occurs when the application is closing.
    /// </summary>
    private async void OnExitAsync(object sender, ExitEventArgs e)
    {
        // Likely won't be rendered, but slow PCs might see it ¯\_(ツ)_/¯
        HomeViewModel.Instance?.Log("Saving all settings...");
        ConfigurationFactory.Instance.SaveAllConfigs();

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

    public static void NavigateTo<NavType>()
    {
        _ = ((MainWindow)Current.MainWindow).Navigate(typeof(NavType));
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

    public static void ForwardKeystrokeToBinding(object? sender)
    {
        if (sender is not TextBox tBox)
        {
            return;
        }

        DependencyProperty prop = TextBox.TextProperty;

        BindingExpression binding = BindingOperations.GetBindingExpression(tBox, prop);
        binding?.UpdateSource();
    }

    public readonly struct Version
    {
        public static string GetFullVersion(Assembly? assembly = null)
        {
            return $"{VersionPrefix}{(assembly ?? Assembly.GetExecutingAssembly()).GetName().Version}";
        }

        public static string GetVersion(Assembly? assembly = null)
        {
            return (assembly ?? Assembly.GetExecutingAssembly()).GetName().Version!.ToString();
        }

        public const string VersionPrefix =
#if DEBUG
            "d";
#elif DEBUG_RELEASE
            "pd";
#else
            "v";
#endif

        public const string VersionIdentifier =
#if DEBUG
            "Debug";
#elif DEBUG_RELEASE
            "PublicDebug";
#else
            "Release";
#endif

        public const VersionTypeValue VersionType =
#if DEBUG
            VersionTypeValue.Debug;
#elif DEBUG_RELEASE
            VersionTypeValue.PublicDebug;
#else
            VersionTypeValue.Release;
#endif

        public const bool IsDebugRelease =
#if DEBUG || DEBUG_RELEASE
                true;
#else
                false;
#endif

        public enum VersionTypeValue
        {
            Release,
            PublicDebug,
            Debug,
        }
    }
}