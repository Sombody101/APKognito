using System.Windows.Threading;
using APKognito.AdbTools;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Controls.ViewModels;
using APKognito.Models;
using APKognito.Utilities;
using Wpf.Ui.Abstractions.Controls;

namespace APKognito.Controls;

/// <summary>
/// Interaction logic for AndroidDeviceInfo.xaml
/// </summary>
public partial class AndroidDeviceInfo : INavigableView<AndroidDeviceInfoViewModel>
{
    private const int UPDATE_DELAY_MS = 10_000;
    private const int GB_DIVIDER = 1024 * 1024;

    private static readonly AdbConfig s_adbConfig = App.GetService<ConfigurationFactory>()!.GetConfig<AdbConfig>();

    private static AndroidDeviceInfoViewModel s_viewModel = null!;

    public static readonly DependencyProperty AndroidDeviceProperty =
        DependencyProperty.Register(nameof(AndroidDevice), typeof(AndroidDevice), typeof(AndroidDeviceInfo)
    );

    public static readonly DependencyProperty RenderTypeProperty =
        DependencyProperty.Register(nameof(RenderType), typeof(InfoRenderType), typeof(AndroidDeviceInfo)
    );

    public static readonly RoutedEvent TriggeredEvent =
        EventManager.RegisterRoutedEvent(nameof(TriggeredEvent), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(AndroidDeviceInfo)
    );

    public static readonly DependencyProperty TriggeredEventProperty =
        DependencyProperty.Register(nameof(TriggeredEvent), typeof(RoutedEvent), typeof(AndroidDeviceInfo)
    );

    public AndroidDevice AndroidDevice
    {
        get => (AndroidDevice)GetValue(AndroidDeviceProperty);
        private set
        {
            s_viewModel.AndroidDevice = value;
            SetValue(AndroidDeviceProperty, value);
        }
    }

    public InfoRenderType RenderType
    {
        get => (InfoRenderType)GetValue(RenderTypeProperty);
        set => SetValue(RenderTypeProperty, value);
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

    public AndroidDeviceInfoViewModel ViewModel => s_viewModel;

    public AndroidDeviceInfo()
        : this(InfoRenderType.Default)
    {
        // For designer
    }

    public AndroidDeviceInfo(InfoRenderType renderType)
    {
        DataContext = this;
        RenderType = renderType;
        CreateViewModel();

        InitializeComponent();

        Loaded += async (sender, e) => await s_viewModel.RefreshDevicesListAsync(true);

        StartDeviceTimer();
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
            await s_viewModel.RefreshDevicesListAsync();
            _dropdownDebounce = false;
        });
    }

    [SuppressMessage("Minor Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "Used for event")]
    private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ForceTick();
    }

    private static Timer? s_deviceUpdateTimer { get; set; }
    private static CancellationTokenSource? s_cts { get; set; }
    private void StartDeviceTimer()
    {
        if (s_deviceUpdateTimer is not null)
        {
            ForceTick();
            return;
        }

        s_deviceUpdateTimer = new Timer(async (sender) =>
        {
            if (s_cts is not null)
            {
                return;
            }

            s_cts = new();
            s_cts.CancelAfter(UPDATE_DELAY_MS - 1000);

            try
            {
                AndroidDevice? device = await UpdateDeviceInfoAsync(s_cts.Token);
                _ = await Dispatcher.InvokeAsync(() => AndroidDevice = device ?? AndroidDevice.Empty);
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
                s_cts?.Dispose();
                s_cts = null;
            }
        }, null, 0, UPDATE_DELAY_MS);

        ForceTick();
    }

    public static void ForceTick()
    {
        if (s_deviceUpdateTimer is null)
        {
            return;
        }

        _ = s_deviceUpdateTimer.Change(0, 1);
        _ = s_deviceUpdateTimer.Change(0, UPDATE_DELAY_MS);
    }

    private static async Task<AndroidDevice?> UpdateDeviceInfoAsync(CancellationToken token = default)
    {
        AdbDeviceInfo? device = s_adbConfig.GetCurrentDevice();

        if (device is null)
        {
            return null;
        }

        // Get battery charge (Android 15 gives a whole lot more information about the battery, so trim everything but the first line)
        AdbCommandOutput result = await AdbManager.QuickDeviceCommandAsync("shell dumpsys battery | grep 'level' | cut -d ':' -f 2 | head -n 1", token: token, noThrow: true);

        if (result.Errored)
        {
            return AndroidDevice.Empty;
        }

        if (!int.TryParse(result.StdOut, out int batteryPercentage))
        {
            batteryPercentage = -1;
        }

        string rawStorageInfo = (await AdbManager.QuickDeviceCommandAsync("shell df | grep -E '^(/dev/block|rootfs|tmp)'", token: token)).StdOut;
        (float total, float used, float free) = ParseDeviceStorage(rawStorageInfo);

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

    private static void CreateViewModel()
    {
        s_viewModel ??= new();
    }

}

public enum InfoRenderType
{
    Default,
    Expander,
    SideMenu,
}
