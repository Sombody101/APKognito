using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Models;
using APKognito.Utilities;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Wpf.Ui;

namespace APKognito.ViewModels.Pages;

public partial class AdbConfigurationViewModel : LoggableObservableObject, IViewable
{
    public static AdbConfigurationViewModel Instance { get; private set; }

    private readonly AdbConfig adbConfig = ConfigurationFactory.GetConfig<AdbConfig>();
    private readonly AdbHistory adbHistory = ConfigurationFactory.GetConfig<AdbHistory>();

    #region Properties

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
        Instance = this;
    }

    #region Commands

    [RelayCommand]
    private void OnSetOverridePaths()
    {
        adbConfig.GetCurrentDevice()!.InstallPaths = new(OverrideObbPath);
        ConfigurationFactory.SaveConfig(adbConfig);
    }

    #endregion Commands

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

    private void UpdateDeviceOptions(AdbDeviceInfo currentDevice)
    {
        // Allow for controls to update their values, even if they're disabled right after
        SelectedDeviceType = DeviceTypeList.First(type => type.Value == currentDevice.DeviceType);
        OverrideObbPath = currentDevice.InstallPaths.ObbPath;

        RefreshItemEligibility(currentDevice);
    }

    public static void OnDeviceChanged(AdbDeviceInfo currentDevice)
    {
        Instance?.UpdateDeviceOptions(currentDevice);
    }
}

public enum AdbDevicesStatus
{
    NoAdb,
    NoDevices,
    DefaultDeviceSelected,
    TooManyDevices,
}