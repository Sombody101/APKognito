using APKognito.AdbTools;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Utilities.MVVM;
using System.Runtime.InteropServices;
using Wpf.Ui;

namespace APKognito.ViewModels.Pages;

public partial class AdbConfigurationViewModel : LoggableObservableObject
{
    private readonly AdbConfig adbConfig;

    #region Properties

    // Field visibility

    [ObservableProperty]
    public partial bool DevicePropertiesEnabled { get; set; } = false;

    [ObservableProperty]
    public partial bool OverridePathsEnabled { get; set; } = false;

    [ObservableProperty]
    public partial string OverrideObbPath { get; set; } = string.Empty;

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
        adbConfig = null!;
    }

    public AdbConfigurationViewModel(ISnackbarService _snackbarService, ConfigurationFactory _configFactory)
    {
        SetSnackbarProvider(_snackbarService);
        adbConfig = _configFactory.GetConfig<AdbConfig>();
    }

    #region Commands

    #endregion Commands

    internal static async Task<AdbDevicesStatus> TryConnectDeviceAsync([Optional] AdbConfig? adbConfig)
    {
        if (!AdbManager.AdbWorks())
        {
            return AdbDevicesStatus.NoAdb;
        }

        string[] foundDevices = [.. await AdbManager.GetDeviceListAsync()];

        if (foundDevices.Length is 0)
        {
            return AdbDevicesStatus.NoDevices;
        }

        adbConfig ??= App.GetService<ConfigurationFactory>()!.GetConfig<AdbConfig>();

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
}

public enum AdbDevicesStatus
{
    NoAdb,
    NoDevices,
    DefaultDeviceSelected,
    TooManyDevices,
}