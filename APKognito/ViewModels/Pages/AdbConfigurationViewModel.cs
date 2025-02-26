using APKognito.AdbTools;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using System.Runtime.InteropServices;
using Wpf.Ui;

namespace APKognito.ViewModels.Pages;

public partial class AdbConfigurationViewModel : LoggableObservableObject
{
    private readonly AdbConfig adbConfig = ConfigurationFactory.Instance.GetConfig<AdbConfig>();

    #region Properties

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

        adbConfig ??= ConfigurationFactory.Instance.GetConfig<AdbConfig>();

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