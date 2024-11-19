using APKognito.Utilities;
using System.Windows.Controls;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using Wpf.Ui;
using System.Collections.ObjectModel;
using APKognito.Configurations.ConfigModels;
using APKognito.Configurations;

namespace APKognito.ViewModels.Pages;

public partial class AdbConfigurationViewModel : ObservableObject, IViewable
{
    private readonly ISnackbarService snackbarService;
    private readonly AdbConfig adbConfig = ConfigurationFactory.GetConfig<AdbConfig>();

    #region Properties

    [ObservableProperty]
    private ObservableCollection<ComboBoxItem> _deviceList = [];

    [ObservableProperty]
    private ComboBoxItem _selectedDevice;

    #endregion Properties

    public AdbConfigurationViewModel(ISnackbarService _snackbarService)
    {
        snackbarService = _snackbarService;
    }

    public async Task RefreshDevicesList()
    {
        try
        {
            IEnumerable<string> foundDevices = await AdbManager.GetAllDevices();
            if (!foundDevices.Any())
            {
                snackbarService.Show(
                    "No devices found",
                    "Cannot get any ADB devices (Ensure they're plugged in and have developer mode enabled).",
                    ControlAppearance.Danger,
                    new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24 },
                    TimeSpan.FromSeconds(10)
                );

                return;
            }

            ComboBoxItem[] devices = [.. foundDevices.Select(str => new ComboBoxItem() { Content = str })];

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
                    foreach (ComboBoxItem device in devices)
                    {
                        DeviceList.Add(device);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            snackbarService.Show(
                "Failed to get devices",
                ex.Message,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24 },
                TimeSpan.FromSeconds(10)
            );
        }
    }

    partial void OnSelectedDeviceChanged(ComboBoxItem value)
    {
        if (value is null)
        {
            // The user clicked the combo box to select a new item
            return;
        }

        adbConfig.CurrentDeviceId = SelectedDevice.Content.ToString()!.Split(" -")[0];
    }
}
