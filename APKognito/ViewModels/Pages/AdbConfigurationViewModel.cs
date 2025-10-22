using System.Runtime.InteropServices;
using APKognito.AdbTools;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Controls.Dialogs;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using Wpf.Ui;

namespace APKognito.ViewModels.Pages;

public partial class AdbConfigurationViewModel : LoggableObservableObject
{
    private readonly AdbConfig _adbConfig;
    private readonly IContentDialogService _contentDialogService;

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
        get => _adbConfig.PlatformToolsPath;
        set
        {
            _adbConfig.PlatformToolsPath = value;
            OnPropertyChanged(nameof(PlatformToolsPath));
        }
    }

    #endregion Properties

    public AdbConfigurationViewModel(
        ISnackbarService snackbarService,
        ConfigurationFactory configFactory,
        IContentDialogService dialogService
    ) : base(configFactory)
    {
        SetSnackbarProvider(snackbarService);
        _adbConfig = configFactory.GetConfig<AdbConfig>();
        _contentDialogService = dialogService;
    }

    public AdbConfigurationViewModel()
    {
        // For designer
        _adbConfig = null!;
        _contentDialogService = null!;
    }

    #region Commands

    [RelayCommand]
    private async Task OnRunCardCommandAsync(string command)
    {
        try
        {
            await RunDialogCommandAsync(command);
        }
        catch (Exception ex)
        {
            SnackError(ex.Message);
            FileLogger.LogException(ex);
        }
    }

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

    private async Task RunDialogCommandAsync(string commandPair)
    {
        // command|Title
        string[] split = commandPair.Split('|', 2);
        string command = split[0];
        string description = split[1];

        if (!await ConfirmToolInstallAsync(description))
        {
            return;
        }

        WPFUI.Controls.ContentDialogResult dialogResult = await ConsoleDialog.RunInternalCommandAsync(
            command,
            "Utility Installer",
            "Cancel Install",
            "Close Installer",
            _contentDialogService.GetDialogHost()
        );

        if (dialogResult is Wpf.Ui.Controls.ContentDialogResult.Primary)
        {
            SnackError("Tool installation canceled", $"{description} was not installed because the installation was canceled.");
        }
    }

    private static async Task<bool> ConfirmToolInstallAsync(string description)
    {
        MessageBoxResult confirmation = await new MessageBox()
        {
            Title = "Confirm Tool Install",
            Content = new TextBlock()
            {
                Text = $"Are you sure you'd like to install {description}?",
                Margin = new(0, 0, 0, 5)
            },
            PrimaryButtonText = "Install",
            CloseButtonText = "Cancel"
        }.ShowDialogAsync();

        return confirmation is MessageBoxResult.Primary;
    }
}

public enum AdbDevicesStatus
{
    NoAdb,
    NoDevices,
    DefaultDeviceSelected,
    TooManyDevices,
}
