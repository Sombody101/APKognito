using APKognito.Configurations.ConfigModels;
using APKognito.ViewModels.Controls;

namespace APKognito.Controls;

/// <summary>
/// Interaction logic for AdbDeviceInfo.xaml
/// </summary>
public partial class AdbDeviceInfoBox : IViewable
{
    #region Properties

    public static readonly DependencyProperty DeviceSourceProperty = DependencyProperty.Register(
        "Device",
        typeof(AdbDeviceInfo),
        typeof(AdbDeviceInfoBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)
    );

    public AdbDeviceInfo DeviceInfo
    {
        get => (AdbDeviceInfo)GetValue(DeviceSourceProperty);
        set
        {
            SetValue(DeviceSourceProperty, value);
            RefreshDeviceInfo(value);
        }
    }

    #endregion

    public AdbDeviceViewModel ViewModel { get; }

    public AdbDeviceInfoBox()
        : this(new())
    { }

    public AdbDeviceInfoBox(AdbDeviceViewModel viewModel)
    {
        DataContext = this;
        ViewModel = viewModel;

        InitializeComponent();
    }

    private void RefreshDeviceInfo(AdbDeviceInfo device)
    {
    }
}
