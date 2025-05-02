using APKognito.AdbTools;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using Wpf.Ui.Controls;

namespace APKognito.Controls.Dialogs;

/// <summary>
/// Interaction logic for LogpackCreatorDialog.xaml
/// </summary>
public sealed partial class LogpackCreatorDialog : ContentDialog, IDisposable
{
    private readonly AdbConfig adbConfig;
    private readonly Timer _timer;

    public static readonly DependencyProperty IncludeCrashLogsProperty = DependencyProperty.Register(
        nameof(IncludeCrashLogs),
        typeof(bool),
        typeof(LogpackCreatorDialog),
        new PropertyMetadata(true)
    );

    public static readonly DependencyProperty IsAdbEnabledProperty = DependencyProperty.Register(
        nameof(IsAdbEnabled),
        typeof(bool),
        typeof(LogpackCreatorDialog)
    );

    public bool IncludeCrashLogs
    {
        get => (bool)GetValue(IncludeCrashLogsProperty);
        set => SetValue(IncludeCrashLogsProperty, value);
    }

    public bool IsAdbEnabled
    {
        get => (bool)GetValue(IsAdbEnabledProperty);
        set => SetValue(IsAdbEnabledProperty, value);
    }

    public LogpackCreatorDialog(ContentPresenter? contentPresenter)
        : base(contentPresenter)
    {
        DataContext = this;
        InitializeComponent();

        adbConfig = App.GetService<ConfigurationFactory>()!.GetConfig<AdbConfig>();

        _timer = new(async (sender) =>
        {
            await Dispatcher.BeginInvoke(async () =>
            {
                bool newState = adbConfig.GetCurrentDevice() is not null 
                    && (await AdbManager.QuickCommandAsync("shell printf 'hello'", true)).StdOut is "hello";

                IsAdbEnabled = newState;

                if (!newState)
                {
                    PullLogsToggle.IsChecked = false;
                    PullLogsCard.ToolTip = "No ADB enabled device is selected!";
                }
                else
                {
                    PullLogsCard.ToolTip = null;
                }
            });
        }, this, 0, 10_000);
    }

    public LogpackCreatorDialog()
    {
        // For designer
        adbConfig = null!;
        _timer = null!;
    }

    public void Dispose()
    {
        _timer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
