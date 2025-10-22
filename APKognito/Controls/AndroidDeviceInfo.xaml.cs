using APKognito.Controls.ViewModels;
using APKognito.Models;
using Wpf.Ui.Abstractions.Controls;

namespace APKognito.Controls;

/// <summary>
/// Interaction logic for AndroidDeviceInfo.xaml
/// </summary>
public partial class AndroidDeviceInfo : INavigableView<AndroidDeviceInfoViewModel>
{
    // This is more of a singleton for the view, but I don't want it to be accessed via DI.
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

        ViewModel.StartDeviceTimer();
    }

    private bool _dropdownDebounce = false;

    private async void ComboBox_DropDownOpenedAsync(object sender, EventArgs e)
    {
        if (_dropdownDebounce)
        {
            return;
        }

        _dropdownDebounce = true;
        await s_viewModel.RefreshDevicesListAsync();
        _dropdownDebounce = false;
    }

    private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.ForceTick();
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
