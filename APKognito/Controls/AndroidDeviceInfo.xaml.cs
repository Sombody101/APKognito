using APKognito.AdbTools;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Controls.ViewModel;
using APKognito.Models;
using APKognito.Utilities;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace APKognito.Controls;

/// <summary>
/// Interaction logic for AndroidDeviceInfo.xaml
/// </summary>
public partial class AndroidDeviceInfo : INavigableView<AndroidDeviceInfoViewModel>
{
    private const int UPDATE_DELAY_MS = 10_000;
    private const int GB_DIVIDER = 1024 * 1024;

    private static readonly AdbConfig adbConfig = ConfigurationFactory.GetConfig<AdbConfig>();

    private static AndroidDeviceInfoViewModel viewModel = null!;
    private static void CreateViewModel()
    {
        viewModel ??= new();
    }

    public static readonly DependencyProperty AndroidDeviceProperty =
        DependencyProperty.Register(
            "AndroidDevice",
            typeof(AndroidDevice),
            typeof(AndroidDeviceInfo)
    );

    public static readonly DependencyProperty TriggeredEventProperty =
        DependencyProperty.Register(
            "TriggeredEvent",
            typeof(RoutedEvent),
            typeof(AndroidDeviceInfo),
            new PropertyMetadata(null)
    );

    public static readonly RoutedEvent TriggeredEvent =
        EventManager.RegisterRoutedEvent(
            "TriggeredEvent",
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(AndroidDeviceInfo)
    );

    public AndroidDevice AndroidDevice
    {
        get => (AndroidDevice)GetValue(AndroidDeviceProperty);
        private set
        {
            viewModel.AndroidDevice = value;
            SetValue(AndroidDeviceProperty, value);
        }
    }

    public event RoutedEventHandler Triggered
    {
        add => AddHandler(TriggeredEvent, value);
        remove => RemoveHandler(TriggeredEvent, value);
    }

    public void RaiseTriggeredEvent()
    {
        RaiseEvent(new RoutedEventArgs(TriggeredEvent, this));
    }

    public AndroidDeviceInfoViewModel ViewModel => viewModel;

    public AndroidDeviceInfo()
    {
        DataContext = this;
        CreateViewModel();

        InitializeComponent();

        Loaded += async (sender, e) => await viewModel.RefreshDevicesList(true);

        StartDeviceTimer(this);
    }

    private bool _dropdownDebounce = false;
    private void ComboBox_DropDownOpened(object sender, EventArgs e)
    {
        if (_dropdownDebounce)
        {
            return;
        }

        _dropdownDebounce = true;
        _ = Dispatcher.Invoke(async () =>
        {
            await viewModel.RefreshDevicesList();
            _dropdownDebounce = false;
        });
    }

    [SuppressMessage("Minor Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "Used for event")]
    private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ForceTick();
    }

    private static Timer? _deviceUpdateTimer;
    private static CancellationTokenSource? cts;
    private static void StartDeviceTimer(AndroidDeviceInfo instance)
    {
        if (_deviceUpdateTimer is not null)
        {
            ForceTick();
            return;
        }

        _deviceUpdateTimer = new Timer(async (sender) =>
        {
            if (cts is not null)
            {
                return;
            }

            cts = new();
            cts.CancelAfter(UPDATE_DELAY_MS - 1000);

            try
            {
                AndroidDevice? device = await UpdateDeviceInfo(cts.Token);
                _ = await instance.Dispatcher.InvokeAsync(() => instance.AndroidDevice = device ?? AndroidDevice.Empty);
            }
            catch (OperationCanceledException)
            {
                // Ignore
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex);
            }
            finally
            {
                cts.Dispose();
                cts = null;
            }
        }, null, 0, UPDATE_DELAY_MS);

        ForceTick();
    }

    public static void ForceTick()
    {
        if (_deviceUpdateTimer is null)
        {
            return;
        }

        _ = _deviceUpdateTimer.Change(0, 1);
        _ = _deviceUpdateTimer.Change(0, UPDATE_DELAY_MS);
    }

    private static async Task<AndroidDevice?> UpdateDeviceInfo(CancellationToken token = default)
    {
        AdbDeviceInfo? device = adbConfig.GetCurrentDevice();

        if (device is null)
        {
            return null;
        }

        // Get battery charge
        CommandOutput result = await AdbManager.QuickDeviceCommand("shell dumpsys battery | grep 'level' | cut -d ':' -f 2", token: token, noThrow: true);

        if (result.Errored)
        {
            FileLogger.Log($"Failed to run command: {result.StdErr}");
            return AndroidDevice.Empty;
        }

        if (!int.TryParse(result.StdOut, out int batteryPercentage))
        {
            batteryPercentage = -1;
        }

        string output = (await AdbManager.QuickDeviceCommand("shell df | grep -E '^(/dev/block|rootfs|tmp)'")).StdOut;
        (float total, float used, float free) = ParseDeviceStorage(output);

        return new()
        {
            BatteryLevel = batteryPercentage,
            TotalSpace = total,
            UsedSpace = used,
            FreeSpace = free
        };
    }

    private static (float Total, float Used, float Free) ParseDeviceStorage(string output)
    {
        try
        {
            long totalSizeGB = 0,
                usedSizeGB = 0,
                freeSizeGB = 0;

            foreach (string line in output.Split('\n'))
            {
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 4)
                {
                    continue;
                }

                totalSizeGB += long.Parse(parts[1]);
                usedSizeGB += long.Parse(parts[2]);
                freeSizeGB += long.Parse(parts[3]);
            }

            return (
                totalSizeGB / GB_DIVIDER,
                usedSizeGB / GB_DIVIDER,
                freeSizeGB / GB_DIVIDER
            );
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            return (0, 0, 0);
        }
    }
}
