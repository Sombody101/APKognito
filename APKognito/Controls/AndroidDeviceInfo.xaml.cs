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
    private void ComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        ForceTick();
    }

    private static Timer? _deviceUpdateTimer;
    private static void StartDeviceTimer(AndroidDeviceInfo instance)
    {
        if (_deviceUpdateTimer is not null)
        {
            ForceTick();
            return;
        }

        _deviceUpdateTimer = new Timer(async (sender) =>
        {
            try
            {
                AndroidDevice? device = await UpdateDeviceInfo();

                _ = await instance.Dispatcher.InvokeAsync(() => instance.AndroidDevice = device ?? AndroidDevice.Empty);
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex);
            }
        }, null, 0, 10_000);

        ForceTick();
    }

    public static void ForceTick()
    {
        if (_deviceUpdateTimer is null)
        {
            return;
        }

        _ = _deviceUpdateTimer.Change(0, 1);
        _ = _deviceUpdateTimer.Change(0, 10_000);
    }

    private static async Task<AndroidDevice?> UpdateDeviceInfo()
    {
        AdbDeviceInfo? device = adbConfig.GetCurrentDevice();

        if (device is null)
        {
            return null;
        }

        // Get battery charge
        CommandOutput result = await AdbManager.QuickDeviceCommand("shell dumpsys battery | grep 'level' | cut -d ':' -f 2", noThrow: true);

        if (result.Errored)
        {
            FileLogger.Log($"Failed to run command: {result.StdErr}");
            return AndroidDevice.Empty;
        }

        if (!int.TryParse(result.StdOut, out int batteryPercentage))
        {
            batteryPercentage = -1;
        }

        string output = (await AdbManager.QuickDeviceCommand("shell df")).StdOut;
        (float total, float used, float free) = ParseDeviceStorage(output);

        return new()
        {
            // DeviceName = (await AdbManager.QuickDeviceCommand("shell getprop ro.product.model")).StdOut,
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
            string? line = Array.Find(output.Split('\n'), x => x.StartsWith("/data/media") || x.StartsWith("/dev/fuse"));

            if (string.IsNullOrEmpty(line))
            {
                return (0, 0, 0);
            }

            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 4)
            {
                return (0, 0, 0);
            }

            long totalSizeGB = long.Parse(parts[1]);
            long usedSizeGB = long.Parse(parts[2]);
            long freeSizeGB = long.Parse(parts[3]);

            return (
                totalSizeGB / 1024 / 1024,
                usedSizeGB / 1024 / 1024,
                freeSizeGB / 1024 / 1024
            );
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            return (0, 0, 0);
        }
    }
}
