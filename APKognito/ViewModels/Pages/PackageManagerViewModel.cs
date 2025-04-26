using APKognito.AdbTools;
using APKognito.ApkMod;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Controls;
using APKognito.Controls.ViewModels;
using APKognito.Models;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using Ionic.Zip;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using Wpf.Ui;

namespace APKognito.ViewModels.Pages;

public partial class PackageManagerViewModel : LoggableObservableObject
{
    private readonly ConfigurationFactory configFactory;
    private readonly AdbConfig adbConfig;

    private readonly IContentDialogService dialogService;

    private readonly List<PackageEntry> cachedPackageList = [];

    #region Properties

    [ObservableProperty]
    public partial ObservableCollection<PackageEntry> PackageList { get; set; } = [
#if DEBUG
        new("test","/apk.apk", 10923, null, 0, 0),
        new("tet","/a.apk", 23874, "jsjs", 2399, -1),
        new("test twice", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", 209374029374, "rueu", 23098, 023423),
        new("test ttjasdh lkjashdlkhsadkljfhsjdfhlkjsad", "/home/user/k/something.apk", 0004334958374, "test/dddfsd", 38479828, 203984),
        new("test tejasdh lkjashdlkfjkljalsjdfhlkjsad", "/home/userk/something.apk", 0004334958374, "test/dddfsd", 38479828, 203984),
        new("test tesjasdh lkjashdlkfjsdljfhsjdfhlkjsad", "/home/ur/idk/something.apk", 0004334958374, "test/dddfsd", 38479828, 203984),
        new("test tstjasdh lkjashdlkfjhadkljfhajdfhlkjsad", "/home/er/idk/something.apk", 0004334958374, "test/dddfsd", 38479828, 203984),
#endif
    ];

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; } = false;

    [ObservableProperty]
    public partial bool EnableControls { get; set; } = true;

    [ObservableProperty]
    public partial bool EnableProgressBar { get; set; } = false;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int SelectedItems { get; set; } = 0;

    [ObservableProperty]
    public partial string CurrentlyPulling { get; set; } = "None";

    #endregion Properties

    public PackageManagerViewModel()
    {
        // For designer
        dialogService = null!;
        configFactory = null!;
        adbConfig = null!;
    }

    public PackageManagerViewModel(
        ISnackbarService snackService,
        IContentDialogService _dialogService,
        ConfigurationFactory _configFactory
    )
    {
        configFactory = _configFactory;
        adbConfig = configFactory.GetConfig<AdbConfig>();

        dialogService = _dialogService;
        SetSnackbarProvider(snackService);
    }

    #region Commands

    [RelayCommand]
    private async Task OnUpdatePackageListAsync()
    {
        await UpdatePackageListAsync();
    }

    [RelayCommand]
    private async Task OnUninstallPackagesAsync(ListView list)
    {
        try
        {
            await UninstallPackagesAsync(list, false);
            await UpdatePackageListAsync();
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            SnackError("Uninstall failed!", ex.Message);
        }
    }

    [RelayCommand]
    private async Task OnSoftUninstallPackagesAsync(ListView list)
    {
        try
        {
            await UninstallPackagesAsync(list);
            await UpdatePackageListAsync();
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            SnackError("Soft uninstall failed!", ex.Message);
        }
    }

    [RelayCommand]
    private async Task OnPullPackagesAsync(ListView list)
    {
        try
        {
            await PullPackagesAsync(list);
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            SnackError("Package pull failed!", ex.Message);
        }
    }

    [RelayCommand]
    private async Task OnPushPackagesAsync()
    {
        EnableControls = false;

        try
        {
            string? package = PromptForPackage();

            if (package is null)
            {
                return;
            }

            ZipEntry? manifest = new ZipFile(package).Entries.FirstOrDefault(e => e.FileName == "AndroidManifest.xml");

            string packageName = Path.GetFileNameWithoutExtension(package);

            if (manifest is null)
            {
                SnackError("No Manifest!", $"Failed to find a manifest in the package {packageName}");
                return;
            }

            using MemoryStream manifestStream = new();
            manifest.Extract(manifestStream);

            packageName = ApkEditorContext.GetPackageName(manifestStream);

            await AdbManager.WakeDeviceAsync();
            await AdbManager.QuickCommandAsync($@"install -g ""{package}""");

            string assetDirectory = Path.Combine(Path.GetDirectoryName(package)!, packageName);
            if (Directory.Exists(assetDirectory))
            {
                await AdbManager.QuickCommandAsync($@"push ""{assetDirectory}"" ""{AdbManager.ANDROID_OBB}/{packageName}""");
            }
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            SnackError("Package push failed!", ex.Message);
        }
        finally
        {
            EnableControls = true;
        }
    }

    #endregion Commands

    partial void OnSearchTextChanged(string value)
    {
        FilterPackages(value);
    }

    private async Task UninstallPackagesAsync(ListView list, bool softUninstall = true)
    {
        EnableControls = false;

        IEnumerable<PackageEntry> items = list.SelectedItems.Cast<PackageEntry>();
        int selected = items.Count();

        if (selected is 0)
        {
            SnackError("No packages selected!", "Select at least one package to uninstall.");
            EnableControls = true;
            return;
        }

        MessageBoxResult result = await new MessageBox()
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

        AdbDeviceInfo? device = adbConfig.GetCurrentDevice();

        if (device is null)
        {
            SnackError("No device selected!", "You need to select a device before uninstalling any packages.");
            EnableControls = true;
            return;
        }

        try
        {
            await ImplementPackageRemovalAsync(items, softUninstall);

            SnackSuccess($"{selected} packages removed!", $"{selected} were {(softUninstall ? "soft" : string.Empty)} uninstalled successfully!");
        }
        catch (Exception ex)
        {
            SnackError("Failed to uninstall package(s)!", ex.Message);
            FileLogger.LogException(ex);
        }

        EnableControls = true;
    }

    public async Task UpdatePackageListAsync(bool silent = false)
    {
        /*
         * Current format:
         *  <package name>|<package path>|<package size>|<assets size>|<package save data size>
         */

        AdbDeviceInfo? device = adbConfig.GetCurrentDevice();
        if (device is null)
        {
            if (silent)
            {
                return;
            }

            SnackError("No device!", "No ADB device is set! Select one in from the dropdown!");
        }

        AdbCommandOutput result = await AdbManager.InvokeScriptAsync($"{nameof(AdbScripts.GetPackageInfo)}.sh", string.Empty, true);

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
        cachedPackageList.AddRange(
            rawPackages.Select(PackageEntry.ParseEntry)
        );

        DisplayPackages(cachedPackageList);
    }

    private async Task PullPackagesAsync(ListView list)
    {
        EnableControls = false;

        AdbDeviceInfo? device = adbConfig.GetCurrentDevice();
        if (device is null)
        {
            SnackError("No device!", "No ADB device is set! Select one in from the dropdown!");
            return;
        }

        IEnumerable<PackageEntry> items = list.SelectedItems.Cast<PackageEntry>();
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

        Wpf.Ui.Controls.ContentDialogResult result = await directoryDialog.ShowAsync();

        if (result != Wpf.Ui.Controls.ContentDialogResult.Primary)
        {
            EnableControls = true;
            return;
        }

        string outputDirectory = dialogOutput.OutputDirectory;
        configFactory.SaveConfig<KognitoConfig>();

        foreach (PackageEntry package in items)
        {
            CurrentlyPulling = package.PackageName;
            string outputPackagePath = Path.Combine(outputDirectory, package.PackageName);

            if (Directory.Exists(outputPackagePath))
            {
                Directory.Delete(outputPackagePath, true);
            }

            _ = Directory.CreateDirectory(outputPackagePath);

            _ = await AdbManager.QuickDeviceCommandAsync($"pull \"{package.PackagePath}\" \"{Path.Combine(outputPackagePath, $"{package.PackageName}.apk")}\"");

            if (package.AssetPath is null)
            {
                continue;
            }

            CurrentlyPulling = Path.GetFileName(package.AssetPath);
            string outputAssetPath = Path.Combine(outputPackagePath, package.PackageName);
            _ = Directory.CreateDirectory(outputAssetPath);
            _ = await AdbManager.QuickDeviceCommandAsync($"pull \"{AdbManager.ANDROID_OBB}\" \"{outputAssetPath}\"");
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

        List<PackageEntry> matchingPackages = cachedPackageList.Where(package =>
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

            foreach (PackageEntry package in packageEntries)
            {
                PackageList.Add(package);
            }
        });
    }

    private async Task ImplementPackageRemovalAsync(IEnumerable<PackageEntry> items, bool softUninstall)
    {
        string command = $"shell pm uninstall {(softUninstall ? "-k" : string.Empty)}";
        (string, bool)[] packages = [.. items.Select(p => (p.PackageName, p.AssetPath is not null))];

        foreach ((string, bool) packagePair in packages)
        {
            string packageName = packagePair.Item1;
            string assetPath = $"{AdbManager.ANDROID_OBB}/{packageName}";

            if (string.IsNullOrWhiteSpace(packageName))
            {
                SnackError("Empty package name!", "An entry containing an empty package name was found. This package will not be uninstalled to prevent unintended data loss.");
                return;
            }

            FileLogger.Log($"Uninstalling package: {packageName} (soft = {softUninstall})");

            // Remove package
            _ = await AdbManager.QuickDeviceCommandAsync($"{command} {packageName}");

            if (packagePair.Item2)
            {
                // Assets
                _ = await AdbManager.QuickDeviceCommandAsync($"shell rm -r \"{assetPath}\"");
            }

            if (!softUninstall)
            {
                // Save data
                _ = await AdbManager.QuickDeviceCommandAsync($"shell rm -r \"{AdbManager.ANDROID_DATA}/{packageName}\"", noThrow: true);
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
                throw new InvalidOperationException($"Failed to find package `{packageName}` in list.");
            }

            PackageList.RemoveAt(itemInd);
        }
    }

    private string? PromptForPackage()
    {
        OpenFileDialog openFileDialog = new()
        {
            Filter = "APK files (*.apk)|*.apk",
        };

        bool? result = openFileDialog.ShowDialog();

        if (result is null)
        {
            Log("Failed to get file. Please try again.");
            return null;
        }

        if ((bool)result)
        {
            return openFileDialog.FileName;
        }
        else
        {
            Log("Did you forget to select a file from the File Explorer window?");
        }

        return null;
    }
}