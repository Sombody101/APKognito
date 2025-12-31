#if DEBUG
//#define BATTERY_TEST_ANIMATION
#endif

using System.Collections.ObjectModel;
using APKognito.AdbTools;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Models;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;

namespace APKognito.Controls.ViewModels;

public sealed partial class AndroidDeviceInfoViewModel : ObservableObject
{
    private const int UPDATE_DELAY_MS = 10_000;

    private readonly AdbConfig _adbConfig = App.GetService<ConfigurationFactory>()!.GetConfig<AdbConfig>();

    private Timer? DeviceUpdateTimer { get; set; }
    private CancellationTokenSource? Cts { get; set; }

    #region Properties

    [ObservableProperty]
    public partial ObservableCollection<AdbDeviceInfo> DeviceList { get; set; } = [];

    [ObservableProperty]
    public partial AdbDeviceInfo? SelectedDevice { get; set; }

    [ObservableProperty]
    public partial AndroidDevice AndroidDevice { get; set; } = AndroidDevice.Empty;

    [ObservableProperty]
    public partial string BatteryLabelColor { get; set; } = "#0000";

    [ObservableProperty]
    public partial string FormattedBatteryLevel { get; set; } = "?";

    [ObservableProperty]
    public partial int UsedStoragePercent { get; set; } = 0;

    [ObservableProperty]
    public partial string AndroidDeviceName { get; set; } = string.Empty;

    #endregion Properties

    public AndroidDeviceInfoViewModel()
    {
        // For designer

#if BATTERY_TEST_ANIMATION
        bool increment = true;
        int value = 0;

        System.Timers.Timer batteryTimer = new(TimeSpan.FromMilliseconds(25));
        batteryTimer.Elapsed += async (sender, args) =>
        {
            if (App.Current is null)
            {
                batteryTimer.Stop();
                return;
            }

            try
            {
                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    // Watch the memory usage skyrocket with this bad boy
                    AndroidDevice device = (AndroidDevice is null
                        ? AndroidDevice.Empty
                        : AndroidDevice) with
                    { BatteryLevel = value };

                    OnAndroidDeviceChanged(device);
                });
            }
            catch (TaskCanceledException)
            {
                // Eat
            }

            if (value is 100)
            {
                increment = false;
            }
            else if (value is 0)
            {
                increment = true;
            }

            value = increment
                ? value + 1
                : value - 1;
        };

        batteryTimer.Start();
#endif
    }

    #region Commands

    [RelayCommand]
    private async Task OnTryConnectionAsync()
    {
        try
        {
            _ = await AdbManager.QuickDeviceCommandAsync("shell echo 'Hello, World!'");
        }
        catch
        {
            LoggableObservableObject.GlobalFallbackLogger.SnackError("Device Not Connected", "Device connection test failed. Make sure developer mode is enabled.");
            return;
        }

        LoggableObservableObject.GlobalFallbackLogger.SnackSuccess("Connection Successful", $"{_adbConfig.CurrentDeviceId} is connected.");
    }

    [RelayCommand]
    private static async Task OnTryUploadAdbScriptsAsync()
    {
        await PushAdbScriptsAsync();
    }

    #endregion Commands

    public async Task RefreshDevicesListAsync(bool silent = false)
    {
        try
        {
            IEnumerable<AdbDeviceInfo> foundDevices = await AdbManager.GetLongDeviceListAsync();

            if (!foundDevices.Any())
            {
                if (!silent)
                {
                    LoggableObservableObject.GlobalFallbackLogger?.SnackError(
                        "No devices found",
                        "Cannot get any ADB devices (Ensure they're connected and have developer mode enabled)."
                    );
                }

                return;
            }

            DeviceList.Clear();

            ulong currentDeviceHash = _adbConfig.GetCurrentDevice()?.DeviceHashId ?? 0;
            foreach (AdbDeviceInfo device in foundDevices)
            {
                // The device previously used is available, so use it
                if (device.DeviceHashId == currentDeviceHash)
                {
                    SelectedDevice = device;
                }

                DeviceList.Add(device);
            }
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);

            if (!silent)
            {
                LoggableObservableObject.GlobalFallbackLogger?.SnackError("Failed to get devices", ex.Message);
            }
        }
    }

    public void StartDeviceTimer()
    {
        if (DeviceUpdateTimer is not null)
        {
            ForceTick();
            return;
        }

        DeviceUpdateTimer = new Timer(async (sender) =>
        {
            if (Cts is not null)
            {
                return;
            }

            Cts = new();
            Cts.CancelAfter(UPDATE_DELAY_MS - 1000);

            try
            {
                AndroidDevice? device = await UpdateDeviceInfoAsync(Cts.Token);
                AndroidDevice = device ?? AndroidDevice.Empty;
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
                Cts?.Dispose();
                Cts = null;
            }
        }, null, 0, UPDATE_DELAY_MS);

        ForceTick();
    }

    public void ForceTick()
    {
        if (DeviceUpdateTimer is null)
        {
            return;
        }

        _ = DeviceUpdateTimer.Change(0, 1);
        _ = DeviceUpdateTimer.Change(0, UPDATE_DELAY_MS);
    }

    partial void OnSelectedDeviceChanged(AdbDeviceInfo? value)
    {
        if (value is null)
        {
            // The user clicked the combo box to select a new item
            return;
        }

        _adbConfig.CurrentDeviceId = value.DeviceId;

        // Check that the device has been selected, create new profile if not
        var currentDevice = _adbConfig.GetCurrentDevice();

        if (currentDevice is null)
        {
            _adbConfig.AdbDevices.Add(value);
            LoggableObservableObject.GlobalFallbackLogger?.SnackInfo("New device detected!", $"A new ADB device profile has been created for {value.DeviceId}");
        }
    }

    partial void OnAndroidDeviceChanged(AndroidDevice value)
    {
        BatteryLabelColor = MapBatteryColor(value.BatteryLevel);

        if (value.BatteryLevel < 0)
        {
            FormattedBatteryLevel = "?";
            return;
        }

        FormattedBatteryLevel = $"{value.BatteryLevel}%";
        AndroidDeviceName = value.DeviceName;
    }

    private async Task<AndroidDevice?> UpdateDeviceInfoAsync(CancellationToken token = default)
    {
        AdbDeviceInfo? device = _adbConfig.GetCurrentDevice();

        if (device is null)
        {
            return null;
        }

        const string getStorageScript = "df | awk '/^(\\/dev\\/block|rootfs|tmp)/ {print $2, $3, $4}'";
        const string getBatteryScript = "dumpsys battery | awk -F ':' '/level/ {print $2}'";
        const string getDeviceName = "getprop ro.product.model";

        AdbCommandOutput output = await AdbManager.QuickDeviceCommandAsync($"shell {getDeviceName}; {getBatteryScript}; {getStorageScript}", token: token, noThrow: true);

        if (output.Errored)
        {
            return AndroidDevice.Empty;
        }

        string[] outputLines = output.StdOut.Trim().Split('\n');

        if (outputLines.Length is < 2)
        {
            // Something is fucky if this happens
            FileLogger.LogError("Shell script returned no package or storage information.");
            return AndroidDevice.Empty;
        }

        if (!int.TryParse(outputLines[1], out int batteryPercentage))
        {
            batteryPercentage = -1;
        }

        (float total, float used, float free) = ParseDeviceStorage(outputLines[2..]);

        return new()
        {
            DeviceName = outputLines[0],
            BatteryLevel = batteryPercentage,
            TotalSpace = total,
            UsedSpace = used,
            FreeSpace = free
        };
    }

    private static (float Total, float Used, float Free) ParseDeviceStorage(string[] infoLines)
    {
        const int GB_DIVIDER = 1024 * 1024;

        try
        {
            long totalSizeGB = 0,
                usedSizeGB = 0,
                freeSizeGB = 0;

            foreach (string line in infoLines)
            {
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                totalSizeGB += long.Parse(parts[0]);
                usedSizeGB += long.Parse(parts[1]);
                freeSizeGB += long.Parse(parts[2]);
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

    private static async Task PushAdbScriptsAsync()
    {
        try
        {
            (int pushedCount, int scriptCount, AdbManager.ScriptPushResult result) = await AdbManager.UploadAdbScriptsAsync();

            if (result == AdbManager.ScriptPushResult.Success)
            {
                LoggableObservableObject.GlobalFallbackLogger?.SnackSuccess("Scripts pushed!", $"{pushedCount}/{scriptCount} ADB scripts were pushed successfully!");
                return;
            }

            LoggableObservableObject.GlobalFallbackLogger?.SnackError("Script push failed!", $"{pushedCount}/{scriptCount} scripts failed to be pushed to your device!\nError: {result}");
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            LoggableObservableObject.GlobalFallbackLogger?.SnackError("Unexpected error!", $"An unexpected error occurred while pushing ADB scripts: {ex.Message}");
        }
    }

    private static string MapBatteryColor(int value)
    {
        // fml

        if (value <= 0)
        {
            return "#0000";
        }
        else if (value > 100)
        {
            return "#00FF00";
        }

        int red;
        int green;

        if (value <= 50)
        {
            red = 255;
            green = (int)(value / 50.0 * 255);
        }
        else
        {
            green = 255;
            red = (int)((100 - value) / 50.0 * 255);
        }

        red = Math.Max(0, Math.Min(255, red));
        green = Math.Max(0, Math.Min(255, green));

        return $"#{red:X2}{green:X2}00";
    }
}
