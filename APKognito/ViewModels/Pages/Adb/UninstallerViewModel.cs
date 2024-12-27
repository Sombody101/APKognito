using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Models;
using APKognito.Utilities;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using Wpf.Ui;

namespace APKognito.ViewModels.Pages;

public partial class UninstallerViewModel : LoggableObservableObject, IViewable
{
    private readonly AdbConfig adbConfig = ConfigurationFactory.GetConfig<AdbConfig>();

    private readonly List<PackageEntry> cachedPackageList = [];

    #region Properties

    [ObservableProperty]
    private ObservableCollection<PackageEntry> _packageList = [
#if DEBUG
        new("test", 10923, null, 0, 0),
        new("test", 23874, "jsjs", 2399, -1),
        new("test twice", 209374029374, "rueu", 23098, 023423),
        new("test testjasdh lkjashdlkfjhsadkljfhalsjdfhlkjsad", 0004334958374, "test/dddfsd", 38479828, 203984),
        new("test testjasdh lkjashdlkfjhsadkljfhalsjdfhlkjsad", 0004334958374, "test/dddfsd", 38479828, 203984),
        new("test testjasdh lkjashdlkfjhsadkljfhalsjdfhlkjsad", 0004334958374, "test/dddfsd", 38479828, 203984),
        new("test testjasdh lkjashdlkfjhsadkljfhalsjdfhlkjsad", 0004334958374, "test/dddfsd", 38479828, 203984),
#endif
    ];

    [ObservableProperty]
    private double _listHeight = 500;

    [ObservableProperty]
    private bool _isRefreshing = false;

    [ObservableProperty]
    private bool _enableControls = true;

    [ObservableProperty]
    private bool _enableProgressBar = false;

    [ObservableProperty]
    private string _searchText = string.Empty;

    #endregion Properties

    public UninstallerViewModel()
    {
    }

    public UninstallerViewModel(ISnackbarService snackService)
    {
        SetSnackbarProvider(snackService);

        WindowSizeChanged += (sender, e) =>
        {
            ListHeight = WindowHeight - TitlebarHeight - 225;
        };
    }

    #region Commands

    [RelayCommand]
    private async Task OnUpdatePackageList()
    {
        await UpdatePackageList();
    }

    [RelayCommand]
    private async Task OnUninstallPackages(ListView list)
    {
        await UninstallPackages(list, false);
    }

    [RelayCommand]
    private async Task OnSoftUninstallPackages(ListView list)
    {
        await UninstallPackages(list);
    }

    #endregion Commands

    partial void OnSearchTextChanged(string value)
    {
        FilterPackages(value);
    }

    private async Task UninstallPackages(ListView list, bool softUninstall = true)
    {
        EnableControls = true;

        var items = list.SelectedItems.Cast<PackageEntry>();
        int selected = items.Count();
        string postfix = selected is 1 ? string.Empty : "s";

        var result = await new MessageBox()
        {
            Title = "Are you sure?",
            Content = $"This will {(softUninstall
                ? "remove the APKs and asset files, but not save data, for"
                : "completely remove all data associated with")} the following package{postfix}:\n  ⚬  {string.Join("\n  ⚬  ", items.Select(item => item.PackageName))}\n\nContinue?",
            PrimaryButtonText = $"Uninstall {selected} app{postfix}",
            CloseButtonText = "Cancel"
        }.ShowDialogAsync();

        if (result != MessageBoxResult.Primary)
        {
            EnableControls = false;
            return;
        }

        var device = adbConfig.GetCurrentDevice();

        if (device is null)
        {
            SnackError("No device selected!", "You need to select a device before uninstalling any packages.");
            return;
        }

        string command = $"shell pm uninstall {(softUninstall ? "-k" : string.Empty)}";

        try
        {
            foreach (string entryName in items.Select(package => package.PackageName))
            {
                if (string.IsNullOrWhiteSpace(entryName))
                {
                    SnackError("Empty package name!", "An entry containing an empty package name was found. This package will not be uninstalled to prevent unintended data loss.");
                    return;
                }

                FileLogger.Log($"Uninstalling package: {entryName} (soft = {softUninstall})");

                // Remove package
                await AdbManager.QuickDeviceCommand($"{command} {entryName}");

                if (!softUninstall)
                {
                    // Remove assets
                    await AdbManager.QuickDeviceCommand($"shell rm -r \"/storage/emulated/0/Android/obb/{entryName}\"");

                    // Remove save data
                    await AdbManager.QuickDeviceCommand($"shell rm -r \"/storage/emulated/0/Android/data/{entryName}\"");
                }
            }
        }
        catch (Exception ex)
        {
            SnackError("Failed to uninstall package(s)!", ex.Message);
            FileLogger.LogException(ex);
        }

        EnableControls = false;
    }

    private async Task UpdatePackageList()
    {
        /*
         * Current format:
         *  <package name>|<package size>|<assets size>|<package save data size>
         */

        var device = adbConfig.GetCurrentDevice();
        if (device is null)
        {
            SnackError("No device!", "No ADB device is set! Select one in from the dropdown!");
            return;
        }

        CommandOutput result = await AdbManager.InvokeScript($"{nameof(AdbScripts.GetPackageInfo)}.sh", string.Empty, true);

        if (result.Errored)
        {
            if (result.StdOut.Trim().EndsWith("No such file or directory"))
            {
                SnackError("No ADB scripts!", "ADB scripts have not been installed on this device. Install them by pressing 'Upload ADB Scripts' at the top.");
            }

            SnackError("Unable to get packages!", result.StdErr);
            return;
        }

        string[] rawPackages = [.. result.StdOut.Split('\n').Skip(1).SkipLast(1)];

        cachedPackageList.Clear();
        cachedPackageList.AddRange(rawPackages.Select(adbPackage =>
        {
            string[] split = adbPackage.Split('|');
            if (split.Length != 4)
            {
                return new PackageEntry("[Invalid Format]", -1, null, -1, -1);
            }

            if (!long.TryParse(split[1], out long packageSize))
            {
                packageSize = -1;
            }

            if (!long.TryParse(split[2], out long assetsSize))
            {
                assetsSize = -1;
            }

            if (!long.TryParse(split[3], out long saveDataSize))
            {
                saveDataSize = -1;
            }

            string packageName = split[0];
            string? assetsPath = assetsSize is -1
                ? null
                : $"/storage/emulated/0/Android/obb/{packageName}";

            return new PackageEntry(packageName, packageSize * 1024, assetsPath, assetsSize * 1024, saveDataSize * 1024);
        }));

        DisplayPackages(cachedPackageList);
    }

    private void FilterPackages(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            DisplayPackages(cachedPackageList);
            return;
        }

        string[] filterParts = filter.Split();

        var matchingPackages = cachedPackageList.Where(package =>
        {
            string packageName = package.PackageName;
            foreach (string filterPart in filterParts)
            {
                if (!packageName.Contains(filterPart))
                {
                    return false;
                }
            }

            return true;
        }).ToList();

        DisplayPackages(matchingPackages);
    }

    private void DisplayPackages(List<PackageEntry> packageEntries)
    {
        Dispatcher.CurrentDispatcher.Invoke(() =>
        {
            PackageList.Clear();

            foreach (var package in packageEntries)
            {
                PackageList.Add(package);
            }
        });
    }
}