using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Models;
using APKognito.Utilities;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace APKognito.ViewModels.Pages;

public partial class AdbConfigurationViewModel : LoggableObservableObject, IViewable
{
    private readonly AdbConfig adbConfig = ConfigurationFactory.GetConfig<AdbConfig>();
    private readonly AdbHistory adbHistory = ConfigurationFactory.GetConfig<AdbHistory>();

    #region Properties

    [ObservableProperty]
    private ObservableCollection<ComboItemPair<string>> _deviceList = [];

    [ObservableProperty]
    private ComboItemPair<string>? _selectedDevice;

    [ObservableProperty]
    private List<HumanComboBoxItem<DeviceType>> _deviceTypeList = [.. Enum.GetValues(typeof(DeviceType))
        .Cast<DeviceType>()
        // Don't show 'None'
        .Skip(1)
        .Select(type => new HumanComboBoxItem<DeviceType>(type))
    ];

    [ObservableProperty]
    private HumanComboBoxItem<DeviceType> _selectedDeviceType;

    // Field visibility

    [ObservableProperty]
    private bool _devicePropertiesEnabled = false;

    [ObservableProperty]
    private bool _overridePathsEnabled = false;

    [ObservableProperty]
    private string _overrideObbPath = string.Empty;

    public string PlatformToolsPath
    {
        get => adbConfig.PlatformToolsPath;
        set
        {
            adbConfig.PlatformToolsPath = value;
            OnPropertyChanged(nameof(PlatformToolsPath));
        }
    }

    #endregion Properties

    public AdbConfigurationViewModel()
    {
        // For designer
    }

    public AdbConfigurationViewModel(ISnackbarService _snackbarService)
    {
        SetSnackbarProvider(_snackbarService);
    }

    #region Commands

    [RelayCommand]
    private async Task OnTryConnection()
    {
        try
        {
            _ = await AdbManager.QuickDeviceCommand("shell echo 'Hello, World!'");
        }
        catch
        {
            SnackError("Device Not Connected", "Device connection test failed. Make sure developer mode is enabled.");
            return;
        }

        SnackSuccess("Connection Successful", $"{adbConfig.CurrentDeviceId} is connected.");
    }

    [RelayCommand]
    private void OnSetOverridePaths()
    {
        adbConfig.GetCurrentDevice()!.InstallPaths = new(OverrideObbPath);
        ConfigurationFactory.SaveConfig(adbConfig);
    }

    #endregion Commands

    public async Task RefreshDevicesList()
    {
        try
        {
            IEnumerable<string> foundDevices = await AdbManager.GetAllDevices();

            if (!foundDevices.Any())
            {
                SnackError(
                    "No devices found",
                    "Cannot get any ADB devices (Ensure they're plugged in and have developer mode enabled)."
                );

                return;
            }

            ComboItemPair<string>[] devices = [.. foundDevices.Select(str => new ComboItemPair<string>(str, str.Split(" -")[0]))];

            await Dispatcher.CurrentDispatcher.InvokeAsync(() =>
            {
                DeviceList.Clear();

                if (devices.Length is 1)
                {
                    SelectedDevice = devices[0];
                    DeviceList.Add(SelectedDevice);
                }
                else
                {
                    foreach (ComboItemPair<string> device in devices)
                    {
                        // The device previously used is available, so use it
                        if (device.Value == adbConfig.CurrentDeviceId)
                        {
                            SelectedDevice = device;
                        }

                        DeviceList.Add(device);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            SnackError("Failed to get devices", ex.Message);
        }
    }

    internal static async Task<AdbDevicesStatus> TryConnectDevice([Optional] AdbConfig? adbConfig)
    {
        if (!AdbManager.AdbWorks())
        {
            return AdbDevicesStatus.NoAdb;
        }

        string[] foundDevices = [.. await AdbManager.GetDeviceList()];

        if (foundDevices.Length is 0)
        {
            return AdbDevicesStatus.NoDevices;
        }

        adbConfig ??= ConfigurationFactory.GetConfig<AdbConfig>();

        if (adbConfig.CurrentDeviceId is not null && foundDevices.Contains(adbConfig.CurrentDeviceId))
        {
            return AdbDevicesStatus.DefaultDeviceSelected;
        }
        if (foundDevices.Length is 1)
        {
            adbConfig.CurrentDeviceId = foundDevices[0];
            return AdbDevicesStatus.DefaultDeviceSelected;
        }
        else
        {
            // The user will have to select which device to target.
            return AdbDevicesStatus.TooManyDevices;
        }
    }

    partial void OnSelectedDeviceChanged(ComboItemPair<string>? value)
    {
        if (value is null)
        {
            // The user clicked the combo box to select a new item
            return;
        }

        adbConfig.CurrentDeviceId = value.Value;

        // Check that the device has been selected, create new profile if not
        var currentDevice = adbConfig.GetCurrentDevice();

        if (currentDevice is null)
        {
            var newDeviceProfile = new AdbDeviceInfo(
                value.Value,
                value.DisplayName.Contains("Quest")
                    ? DeviceType.MetaQuest
                    : DeviceType.BasicAndroid,
                string.Empty
            );

            adbConfig.AdbDevices.Add(newDeviceProfile);
            currentDevice = newDeviceProfile;

            SnackInfo("New device detected", $"A new ADB device profile has been created for {value.Value}");
        }

        // Allow for controls to update their values, even if they're disabled right after
        SelectedDeviceType = DeviceTypeList.First(type => type.Value == currentDevice.DeviceType);
        OverrideObbPath = currentDevice.InstallPaths.ObbPath;

        RefreshItemEligibility(currentDevice);
    }

    partial void OnSelectedDeviceTypeChanged(HumanComboBoxItem<DeviceType> value)
    {
        var currentDevice = adbConfig.GetCurrentDevice();

        currentDevice!.DeviceType = value.Value;
        currentDevice.InstallPaths = new(value.Value, OverrideObbPath);
        OverrideObbPath = currentDevice.InstallPaths.ObbPath;

        UpdatePathVariables();

        RefreshItemEligibility(adbConfig.GetCurrentDevice());
    }

    private void RefreshItemEligibility(AdbDeviceInfo? deviceInfo)
    {
        // All device options
        DevicePropertiesEnabled = deviceInfo is not null;

        // Override options
        OverridePathsEnabled = DevicePropertiesEnabled
            && deviceInfo!.DeviceType is DeviceType.UserOverridePaths;
    }

    private void UpdatePathVariables()
    {
        // Update path variables for console
        adbHistory.SetVariable("OBB_PATH", OverrideObbPath);
    }
}

public enum AdbDevicesStatus
{
    NoAdb,
    NoDevices,
    DefaultDeviceSelected,
    TooManyDevices,
}