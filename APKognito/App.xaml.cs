#define NO_EXCEPTION_HANDLING

using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Data;
using APKognito.Configurations;
using APKognito.Services;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Pages;
using APKognito.ViewModels.Pages.Debugging;
using APKognito.ViewModels.Windows;
using APKognito.Views.Pages;
using APKognito.Views.Pages.Debugging;
using APKognito.Views.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
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
    private static FrameLockDetector? s_frameLockDetector;

    public static DirectoryInfo AppDataDirectory { get; } = Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), nameof(APKognito)));

    // The.NET Generic Host provides dependency injection, configuration, logging, and other services.
    // https://docs.microsoft.com/dotnet/core/extensions/generic-host
    // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
    // https://docs.microsoft.com/dotnet/core/extensions/configuration
    // https://docs.microsoft.com/dotnet/core/extensions/logging
    private static readonly IHost s_host = Host
        .CreateDefaultBuilder()
        .ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.AddFilter<ConsoleLoggerProvider>("Microsoft.Hosting.Lifetime", Microsoft.Extensions.Logging.LogLevel.Error);
        })
        .ConfigureAppConfiguration(c =>
        {
            _ = c.SetBasePath(AppContext.BaseDirectory);
        })
        .ConfigureServices((__, services) =>
        {
            _ = services.AddHostedService<ApplicationHostService>();

            _ = services.AddSingleton<ConfigurationFactory>();

            _ = services.AddSingleton<ISnackbarService, SnackbarService>()
                .AddSingleton<INavigationViewPageProvider, PageService>()
                .AddSingleton<IThemeService, ThemeService>()
                .AddSingleton<ITaskBarService, TaskBarService>()
                .AddSingleton<INavigationService, NavigationService>()
                .AddSingleton<IContentDialogService, ContentDialogService>()
                .AddTransient<INavigationWindow, MainWindow>()
                .AddTransient<SetupWizardWindow>()
                // .AddTransient<JavaVersionCollector>()
                // Pages
                .AddSingleton<HomePage>()
                .AddTransient<AdbConsolePage>()
                .AddTransient<AdbConfigurationPage>()
                .AddTransient<DriveUsagePage>()
                .AddTransient<FileExplorerPage>()
                .AddTransient<FileUploaderPage>()
                .AddTransient<PackageManagerPage>()
                .AddTransient<RenameConfigurationPage>()
                //.AddTransient<RenamingHistoryPage>()
                .AddTransient<SettingsPage>()
                .AddTransient<LogViewerPage>()
                // Viewmodels
                .AddSingleton<HomeViewModel>()
                .AddSingleton<MainWindowViewModel>()
                .AddSingleton<SharedViewModel>()
                .AddTransient<AdbConfigurationViewModel>()
                .AddTransient<AdbConsoleViewModel>()
                .AddTransient<DriveUsageViewModel>()
                .AddTransient<FileExplorerViewModel>()
                .AddTransient<FileUploaderViewModel>()
                .AddTransient<PackageManagerViewModel>()
                .AddTransient<RenameConfigurationViewModel>()
                // .AddTransient<RenamingHistoryViewModel>()
                .AddTransient<SettingsViewModel>()
                .AddTransient<LogViewerViewModel>();

            // Wizard
            _ = services
                .AddSingleton<SetupWizardWindow>()
                .AddSingleton<SetupWizardViewModel>();

            // Exception window model
            _ = services.AddSingleton<ExceptionWindowViewModel>();

#if RELEASE
            _ = services.AddHostedService<AutoUpdaterService>();
#endif
        }).Build();


    /// <summary>
    /// Gets registered service.
    /// </summary>
    /// <typeparam name="T">Type of the service to get.</typeparam>
    /// <returns>Instance of the service or <see langword="null"/>.</returns>
    public static T? GetService<T>() where T : class
    {
        return s_host.Services.GetService(typeof(T)) as T;
    }

    public static T GetRequiredService<T>() where T : class
    {
        return s_host.Services.GetRequiredService<T>();
    }

    /// <summary>
    /// Occurs when the application is loading.
    /// </summary>
    private void TimedOnStartup(object sender, StartupEventArgs e)
    {
        Tools.Time(() => OnStartup(sender, e), nameof(OnStartup));
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        FileLogger.Log($"App start. {Version.GetFullVersion()}, {Version.VersionIdentifier}");

        s_frameLockDetector = new(Dispatcher);

#if !NO_EXCEPTION_HANDLING || RELEASE
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                _ = ExceptionWindow.CreateNewExceptionWindow(
                    e.Exception,
                    (ExceptionWindowViewModel)s_host.Services.GetService(typeof(ExceptionWindowViewModel))!,
                    "AppMain [src: TaskScheduler]");
            });
        };

        Dispatcher.UnhandledException += (sender, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                _ = ExceptionWindow.CreateNewExceptionWindow(
                    e.Exception,
                    s_host.Services.GetService<ExceptionWindowViewModel>()!,
                    "AppMain [src: Default Dispatcher]");
            });
        };
#endif

        Tools.Time(s_host.Start, "Host.Start");
        ApplicationAccentColorManager.ApplySystemAccent();

#if DEBUG
        FileLogger.Log("Update service disabled. Use a Public Debug or full Release to get updates.");
#endif
    }

    /// <summary>
    /// Occurs when the application is closing.
    /// </summary>
    private async void OnExitAsync(object sender, ExitEventArgs e)
    {
        // Likely won't be rendered, but slow PCs might see it ¯\_(ツ)_/¯
        LoggableObservableObject.GlobalFallbackLogger?.Log("Saving all settings...");
        GetService<ConfigurationFactory>()!.SaveAllConfigs();

        await s_host.StopAsync();

        s_host.Dispose();
        s_frameLockDetector?.Dispose();
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

    public static void NavigateTo(Type target)
    {
        _ = GetRequiredService<INavigationService>().Navigate(target);
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
                    //Content = $"Failed to open directory as it does not exist.\n\n{directory}",
                    Content = new StackPanel()
                    {
                        Children =
                        {
                            new WPFUI::Controls.TextBlock() { Text = "A directory failed to be opened in explorer as it does not exist.", Margin = new(0,10,0,0) },
                            new WPFUI::Controls.TextBox() { Text = directory, IsReadOnly = true, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new(0,10,0,0) }
                        }
                    }
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
        public static Assembly Assembly => field ??= typeof(Version).Assembly;

        public static string GetFullVersion(Assembly? assembly = null)
        {
            return $"{VersionPrefix}{(assembly ?? Assembly).GetName().Version}";
        }

        public static string GetStringVersion(Assembly? assembly = null)
        {
            return GetVersion(assembly).ToString();
        }

        public static System.Version GetVersion(Assembly? assembly = null)
        {
            return (assembly ?? Assembly).GetName().Version!;
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
