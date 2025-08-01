using APKognito.AdbTools;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Models;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace APKognito.Controls.ViewModels;

public sealed partial class AndroidDeviceInfoViewModel : ObservableObject
{
    private readonly AdbConfig _adbConfig = App.GetService<ConfigurationFactory>()!.GetConfig<AdbConfig>();

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
    public partial int BatteryLevelWidth { get; set; } = 0;

    [ObservableProperty]
    public partial int UsedStoragePercent { get; set; } = 0;

    [ObservableProperty]
    public partial AdbDeviceInfo AdbDeviceInfo { get; set; } = null!;

    #endregion Properties

    public AndroidDeviceInfoViewModel()
    {
        // For designer
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
            LoggableObservableObject.CurrentLoggableObject.SnackError("Device Not Connected", "Device connection test failed. Make sure developer mode is enabled.");
            return;
        }

        LoggableObservableObject.CurrentLoggableObject.SnackSuccess("Connection Successful", $"{_adbConfig.CurrentDeviceId} is connected.");
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
            AdbDeviceInfo[] foundDevices = await AdbManager.GetAllDevicesAsync();

            if (foundDevices.Length is 0)
            {
                if (!silent)
                {
                    LoggableObservableObject.CurrentLoggableObject?.SnackError(
                        "No devices found",
                        "Cannot get any ADB devices (Ensure they're connected and have developer mode enabled)."
                    );
                }

                return;
            }

            await Dispatcher.CurrentDispatcher.InvokeAsync(() =>
            {
                DeviceList.Clear();

                if (foundDevices.Length is 1 && foundDevices[0].DeviceAuthorized)
                {
                    SelectedDevice = foundDevices[0];
                    DeviceList.Add(SelectedDevice);
                    return;
                }

                foreach (AdbDeviceInfo device in foundDevices)
                {
                    // The device previously used is available, so use it
                    if (device.DeviceId == _adbConfig.CurrentDeviceId)
                    {
                        SelectedDevice = device;
                    }

                    DeviceList.Add(device);
                }
            });
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);

            if (!silent)
            {
                LoggableObservableObject.CurrentLoggableObject?.SnackError("Failed to get devices", ex.Message);
            }
        }
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
            LoggableObservableObject.CurrentLoggableObject?.SnackInfo("New device detected!", $"A new ADB device profile has been created for {value.DeviceId}");
        }
    }

    partial void OnAndroidDeviceChanged(AndroidDevice value)
    {
        const int storageElementWidth = 298;

        BatteryLabelColor = MapBatteryColor(value.BatteryLevel);

        if (value.BatteryLevel < 0)
        {
            FormattedBatteryLevel = "?";
            BatteryLevelWidth = 0;
        }
        else
        {
            FormattedBatteryLevel = $"{value.BatteryLevel}%";
            BatteryLevelWidth = value.BatteryLevel;
        }

        UsedStoragePercent = value == AndroidDevice.Empty
            ? 0
            : (int)(storageElementWidth * (value.UsedSpace / value.TotalSpace));
    }

    private static async Task PushAdbScriptsAsync()
    {
        try
        {
            (int pushedCount, int scriptCount, AdbManager.ScriptPushResult result) = await AdbManager.UploadAdbScriptsAsync();

            if (result == AdbManager.ScriptPushResult.Success)
            {
                LoggableObservableObject.CurrentLoggableObject?.SnackSuccess("Scripts pushed!", $"{pushedCount}/{scriptCount} ADB scripts were pushed successfully!");
                return;
            }

            LoggableObservableObject.CurrentLoggableObject?.SnackError("Script push failed!", $"{pushedCount}/{scriptCount} scripts failed to be pushed to your device!\nError: {result}");
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            LoggableObservableObject.CurrentLoggableObject?.SnackError("Unexpected error!", $"An unexpected error occurred while pushing ADB scripts: {ex.Message}");
        }
    }

    private static string MapBatteryColor(int value)
    {
        if (value < 0)
        {
            return "#0000";
        }

        int red = (int)(255 * (100 - value) / 100.0);
        int green = (int)(255 * value / 100.0);

        return $"#{red:X2}{green:X2}00";
    }
}
