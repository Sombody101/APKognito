using APKognito.AdbTools;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Controls;
using APKognito.Controls.ViewModel;
using APKognito.Models;
using APKognito.Models.Settings;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using Wpf.Ui;

namespace APKognito.ViewModels.Pages;

public partial class PackageManagerViewModel : LoggableObservableObject
{
    private readonly AdbConfig adbConfig = ConfigurationFactory.GetConfig<AdbConfig>();

    private readonly IContentDialogService dialogService;

    private readonly List<PackageEntry> cachedPackageList = [];

    #region Properties

    [ObservableProperty]
    private ObservableCollection<PackageEntry> _packageList = [
#if DEBUG
        new("test","/apk.apk", 10923, null, 0, 0),
        new("test","/apk.apk", 23874, "jsjs", 2399, -1),
        new("test twice", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", 209374029374, "rueu", 23098, 023423),
        new("test testjasdh lkjashdlkfjhsadkljfhalsjdfhlkjsad", "/home/user/idk/something.apk", 0004334958374, "test/dddfsd", 38479828, 203984),
        new("test testjasdh lkjashdlkfjhsadkljfhalsjdfhlkjsad", "/home/user/idk/something.apk", 0004334958374, "test/dddfsd", 38479828, 203984),
        new("test testjasdh lkjashdlkfjhsadkljfhalsjdfhlkjsad", "/home/user/idk/something.apk", 0004334958374, "test/dddfsd", 38479828, 203984),
        new("test testjasdh lkjashdlkfjhsadkljfhalsjdfhlkjsad", "/home/user/idk/something.apk", 0004334958374, "test/dddfsd", 38479828, 203984),
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

    [ObservableProperty]
    private int _selectedItems = 0;

    [ObservableProperty]
    private string _currentlyPulling = "None";

    #endregion Properties

    public PackageManagerViewModel()
    {
        // For designer
    }

    public PackageManagerViewModel(ISnackbarService snackService, IContentDialogService _dialogService)
    {
        dialogService = _dialogService;
        SetSnackbarProvider(snackService);
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
        try
        {
            await UninstallPackages(list, false);
            await UpdatePackageList();
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            SnackError("Uninstall failed!", ex.Message);
        }
    }

    [RelayCommand]
    private async Task OnSoftUninstallPackages(ListView list)
    {
        try
        {
            await UninstallPackages(list);
            await UpdatePackageList();
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            SnackError("Soft uninstall failed!", ex.Message);
        }
    }

    [RelayCommand]
    private async Task OnPullPackages(ListView list)
    {
        try
        {
            await PullPackages(list);
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            SnackError("Package pull failed!", ex.Message);
        }
    }

    #endregion Commands

    partial void OnSearchTextChanged(string value)
    {
        FilterPackages(value);
    }

    private async Task UninstallPackages(ListView list, bool softUninstall = true)
    {
        EnableControls = false;

        var items = list.SelectedItems.Cast<PackageEntry>();
        int selected = items.Count();

        if (selected is 0)
        {
            SnackError("No packages selected!", "Select at least one package to uninstall.");
            EnableControls = true;
            return;
        }

        var result = await new MessageBox()
        {
            Title = "Are you sure?",
            Content = $"This will {(softUninstall
                ? "remove the APKs and asset files, but not save data, for"
                : "completely remove all data associated with")} the following {"package".PluralizeIfMany(selected)}:\n  ⚬  {string.Join("\n  ⚬  ", items.Select(item => item.PackageName))}\n\nContinue?",
            PrimaryButtonText = $"{(softUninstall ? "Soft uninstall" : "Uninstall")} {selected} {"app".PluralizeIfMany(selected)}",
            PrimaryButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger,
            CloseButtonText = "Cancel"
        }.ShowDialogAsync();

        if (result != MessageBoxResult.Primary)
        {
            EnableControls = true;
            return;
        }

        var device = adbConfig.GetCurrentDevice();

        if (device is null)
        {
            SnackError("No device selected!", "You need to select a device before uninstalling any packages.");
            EnableControls = true;
            return;
        }

        string command = $"shell pm uninstall {(softUninstall ? "-k" : string.Empty)}";

        try
        {
            (string, bool)[] packages = [.. items.Select(p => (p.PackageName, p.AssetPath is not null))];

            foreach (var package in packages)
            {
                string packageName = package.Item1;
                string assetPath = $"{AdbManager.ANDROID_OBB}/{packageName}";

                if (string.IsNullOrWhiteSpace(packageName))
                {
                    SnackError("Empty package name!", "An entry containing an empty package name was found. This package will not be uninstalled to prevent unintended data loss.");
                    return;
                }

                FileLogger.Log($"Uninstalling package: {packageName} (soft = {softUninstall})");

                // Remove package
                await AdbManager.QuickDeviceCommand($"{command} {packageName}");

                if (package.Item2)
                {
                    // Assets
                    await AdbManager.QuickDeviceCommand($"shell rm -r \"{assetPath}\"");
                }

                if (!softUninstall)
                {
                    // Save data
                    await AdbManager.QuickDeviceCommand($"shell rm -r \"{AdbManager.ANDROID_DATA}/{packageName}\"", noThrow: true);
                }

                int itemInd = 0;
                for (; itemInd < PackageList.Count; ++itemInd)
                {
                    if (PackageList[itemInd].PackageName == packageName)
                    {
                        break;
                    }
                }

                if (itemInd > PackageList.Count)
                {
                    throw new InvalidOperationException("Failed to find package in list.");
                }

                PackageList.RemoveAt(itemInd);
            }

            SnackSuccess($"{selected} packages removed!", $"{selected} were {(softUninstall ? "soft" : string.Empty)} uninstalled successfully!");
        }
        catch (Exception ex)
        {
            SnackError("Failed to uninstall package(s)!", ex.Message);
            FileLogger.LogException(ex);
        }

        EnableControls = true;
    }

    public async Task UpdatePackageList(bool silent = false)
    {
        /*
         * Current format:
         *  <package name>|<package path>|<package size>|<assets size>|<package save data size>
         */

        var device = adbConfig.GetCurrentDevice();
        if (device is null)
        {
            if (silent)
            {
                return;
            }

            SnackError("No device!", "No ADB device is set! Select one in from the dropdown!");
        }

        CommandOutput result = await AdbManager.InvokeScript($"{nameof(AdbScripts.GetPackageInfo)}.sh", string.Empty, true);

        if (result.Errored)
        {
            if (silent)
            {
                return;
            }

            if (result.StdErr.Trim().EndsWith("No such file or directory"))
            {
                SnackError("No ADB scripts!", "ADB scripts have not been installed on this device. Install them by pressing 'Upload ADB Scripts' at the top.");
                return;
            }

            SnackError("Unable to get packages!", result.StdErr);
            return;
        }

        string[] rawPackages = [.. result.StdOut.Split('\n').Skip(1).SkipLast(1)];

        cachedPackageList.Clear();
        cachedPackageList.AddRange(rawPackages.Select(adbPackage =>
        {
            // <package name>|<package path>|<package size in bytes>|<assets size in bytes>|<save data size in bytes>
            string[] split = adbPackage.Split('|');
            if (split.Length != 5)
            {
                return new PackageEntry("[Invalid Format]", string.Empty, -1, null, -1, -1);
            }

            string packageName = split[0];

            string packagePath = split[1];

            if (!long.TryParse(split[2], out long packageSize))
            {
                packageSize = -1;
            }

            if (!long.TryParse(split[3], out long assetsSize))
            {
                assetsSize = -1;
            }

            if (!long.TryParse(split[4], out long saveDataSize))
            {
                saveDataSize = -1;
            }

            string? assetsPath = assetsSize is -1
                ? null
                : $"/sdcard/Android/obb/{packageName}";

            return new PackageEntry(packageName, packagePath, packageSize * 1024, assetsPath, assetsSize * 1024, saveDataSize * 1024);
        }));

        DisplayPackages(cachedPackageList);
    }

    private async Task PullPackages(ListView list)
    {
        EnableControls = false;

        var device = adbConfig.GetCurrentDevice();
        if (device is null)
        {
            SnackError("No device!", "No ADB device is set! Select one in from the dropdown!");
            return;
        }

        var items = list.SelectedItems.Cast<PackageEntry>();
        int selected = items.Count();

        if (selected is 0)
        {
            SnackError("No packages selected!", "Select at least one package to pull.");
            EnableControls = true;
            return;
        }

        DirectoryConfirmationViewModel dialogOutput = new()
        {
            Title = "Directory Confirmation",
            Content = $"This will pull the following {"package".PluralizeIfMany(selected)}:\n  ⚬  {string.Join("\n  ⚬  ", items.Select(item => item.PackageName))}\n\nContinue?",
        };

        DirectoryConfirmationDialog directoryDialog = new(dialogOutput, dialogService.GetDialogHost())
        {
            IsPrimaryButtonEnabled = true,
            PrimaryButtonText = $"Pull {"App".PluralizeIfMany(selected)}",
            PrimaryButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Success
        };

        var result = await directoryDialog.ShowAsync();

        if (result != Wpf.Ui.Controls.ContentDialogResult.Primary)
        {
            EnableControls = true;
            return;
        }

        string outputDirectory = dialogOutput.OutputDirectory;
        ConfigurationFactory.SaveConfig<KognitoConfig>();

        foreach (PackageEntry package in items)
        {
            CurrentlyPulling = package.PackageName;
            string outputPackagePath = Path.Combine(outputDirectory, package.PackageName);

            if (Directory.Exists(outputPackagePath))
            {
                Directory.Delete(outputPackagePath, true);
            }

            Directory.CreateDirectory(outputPackagePath);

            await AdbManager.QuickDeviceCommand($"pull \"{package.PackagePath}\" \"{Path.Combine(outputPackagePath, $"{package.PackageName}.apk")}\"");

            if (package.AssetPath is null)
            {
                continue;
            }

            CurrentlyPulling = Path.GetFileName(package.AssetPath);
            string outputAssetPath = Path.Combine(outputPackagePath, package.PackageName);
            Directory.CreateDirectory(outputAssetPath);
            await AdbManager.QuickDeviceCommand($"pull \"{AdbManager.ANDROID_OBB}\" \"{outputAssetPath}\"");
        }

        CurrentlyPulling = "None";
        EnableControls = true;
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