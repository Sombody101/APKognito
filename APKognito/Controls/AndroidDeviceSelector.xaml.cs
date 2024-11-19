using APKognito.Utilities;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace APKognito.Controls;

/// <summary>
/// Interaction logic for AndroidDeviceSelector.xaml
/// </summary>
public partial class AndroidDeviceSelector
{
    private ComboBoxItem SelectedComboItem;

    public AndroidDeviceSelector()
    {
        InitializeComponent();
        ComboBox_DropDownOpened(null, null);
    }

    public List<ComboBoxItem> DeviceList
    {
        get => (List<ComboBoxItem>)GetValue(DeviceListProperty);
        private set => SetValue(DeviceListProperty, value);
    }

    public static readonly DependencyProperty DeviceListProperty =
        DependencyProperty.Register(
            "DeviceList",
            typeof(List<string>),
            typeof(AndroidDeviceSelector),
            new PropertyMetadata(null)
    );

    public string SelectedDevice
    {
        get => (string)GetValue(SelectedDeviceProperty);
        set => SetValue(SelectedDeviceProperty, value);
    }

    public static readonly DependencyProperty SelectedDeviceProperty =
        DependencyProperty.Register(
            "SelectedDevice",
            typeof(string),
            typeof(AndroidDeviceSelector),
            new PropertyMetadata(null)
    );

    public ICommand TryConnectionCommand { get; set; }

    private void ComboBox_DropDownOpened(object? sender, EventArgs e)
    {

    }

    public async Task RefreshDevicesList(ISnackbarService? snackbarService)
    {
        try
        {
            IEnumerable<string> foundDevices = await AdbManager.GetAllDevices();
            if (!foundDevices.Any())
            {
                snackbarService?.Show(
                    "No devices found",
                    "Cannot get any ADB devices (Ensure they're plugged in and have developer mode enabled).",
                    ControlAppearance.Danger,
                    new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24 },
                    TimeSpan.FromSeconds(10)
                );

                return;
            }

            ComboBoxItem[] devices = [.. foundDevices.Select(str => new ComboBoxItem() { Content = str })];

            await App.Current.Dispatcher.InvokeAsync(() =>
            {
                DeviceList.Clear();

                if (devices.Length is 1)
                {
                    SelectedComboItem = devices[0];
                    DeviceList.Add(SelectedComboItem);
                }
                else
                {
                    DeviceList.AddRange(devices);
                }
            });
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            snackbarService?.Show(
                "Failed to get devices",
                ex.Message,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24 },
                TimeSpan.FromSeconds(10)
            );
        }
    }
}
