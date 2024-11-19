using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Utilities;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Controls;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace APKognito.ViewModels.Pages;

public partial class FileUploaderViewModel : ObservableObject, IViewable
{
    private readonly ISnackbarService snackbarService;
    private readonly AdbConfig adbConfig = ConfigurationFactory.GetConfig<AdbConfig>();

    private List<string> _files = [];

    #region Properties

    [ObservableProperty]
    private ObservableCollection<ListBoxItem> selectedItems = [];

    [ObservableProperty]
    private bool isUploading = false;

    #endregion Properties

    public FileUploaderViewModel(ISnackbarService _snackbarService)
    {
        snackbarService = _snackbarService;
    }

    #region Commands

    [RelayCommand]
    private void OnSelectItems()
    {
        PromptForFiles(ScanType.Overwrite);
    }

    [RelayCommand]
    private void OnAddItems()
    {
        PromptForFiles(ScanType.Append);
    }

    [RelayCommand]
    private async Task OnAddRecurseItems()
    {
        await PromptForRecurseFiles();
    }

    [RelayCommand]
    private async Task OnUploadItemsToDevice()
    {
        if (adbConfig.CurrentDeviceId is null)
        {
            snackbarService.Show(
                 "No device selected",
                "No ADB device has been selected. Go to the ADB Configuration page and choose a device.",
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24 },
                TimeSpan.FromSeconds(10)
            );

            return;
        }

        if (_files.Count is 0)
        {
            snackbarService.Show(
                 "No files selected",
                "No files have been selected to upload.",
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24 },
                TimeSpan.FromSeconds(10)
            );

            return;
        }

        IsUploading = true;

        foreach (string path in _files)
        {
            await AdbManager.QuickCommand($"-s {adbConfig.CurrentDeviceId} push {path} /sdcard/Android/data/");

            FileLogger.Log($"Pushing {_files.Count} files to {adbConfig.CurrentDeviceId}");

            string? assets = GetApkAssetsDirectory(path);
            if (assets is not null)
            {
                string output = await AdbManager.QuickCommand($"-s {adbConfig.CurrentDeviceId} push {assets} /sdcard/Android/obb/");
                FileLogger.Log(output);
            }
        }

        snackbarService.Show(
             "Upload complete",
            "Upload complete",
            ControlAppearance.Success,
            new SymbolIcon { Symbol = SymbolRegular.CheckmarkCircle24 },
            TimeSpan.FromSeconds(10)
        );

        IsUploading = false;
    }

    #endregion

    private void PromptForFiles(ScanType scanType)
    {
        OpenFileDialog openFileDialog = new()
        {
            Filter = "APK files (*.apk)|*.apk",
            Multiselect = true,
        };

        if (openFileDialog.ShowDialog() is false)
        {
            return;
        }

        App.Current.Dispatcher.Invoke(() =>
        {
            if (scanType == ScanType.Overwrite)
            {
                SelectedItems.Clear();
                _files.Clear();
            }

            int duplicatePaths = 0;

            foreach (string path in openFileDialog.FileNames)
            {
                if (_files.Contains(path))
                {
                    duplicatePaths++;
                    continue;
                }

                // Check for an OBB folder
                string? obbPath = FormatObbMessage(path, GetApkAssetsDirectory(path));

                SelectedItems.Add(new() { Content = obbPath });
            }

            _files.AddRange(openFileDialog.FileNames.Where(file => !_files.Contains(file)));

            if (duplicatePaths is not 0)
            {
                snackbarService.Show(
                    $"{duplicatePaths} duplicate path{(duplicatePaths is 1 ? string.Empty : "s")} left out",
                    $"{duplicatePaths}/{openFileDialog.FileNames.Length} paths were not added to the list as they were already in the list.",
                    ControlAppearance.Caution,
                    new SymbolIcon { Symbol = SymbolRegular.Warning24 },
                    TimeSpan.FromSeconds(10)
                );
            }
        });
    }

    private async Task PromptForRecurseFiles()
    {
        OpenFolderDialog openFolderDialog = new();

        if (openFolderDialog.ShowDialog() is false)
        {
            return;
        }

        await App.Current.Dispatcher.InvokeAsync(() =>
        {
            void RecurseSearch(string folder)
            {
                foreach (string path in Directory.GetFiles(folder).Where(path => path.EndsWith(".apk")))
                {
                    // Check for an OBB folder
                    string? obbPath = FormatObbMessage(path, GetApkAssetsDirectory(path));

                    SelectedItems.Add(new() { Content = obbPath });
                }

                foreach (string directory in Directory.GetDirectories(folder))
                {
                    // has things organized.
                    RecurseSearch(directory);
                }
            }

            SelectedItems.Clear();
            _files.Clear();

            RecurseSearch(openFolderDialog.FolderName);
        });
    }

    private static string? GetApkAssetsDirectory(string path)
    {
        string obbDirectory = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));

        if (Directory.Exists(obbDirectory))
        {
            return obbDirectory;
        }

        return null;
    }

    private static string FormatObbMessage(string path, string? obbPath)
    {
        path = Path.GetFileName(path);

        if (obbPath is not null)
        {
            int assetCount = Directory.GetFiles(obbPath).Length;
            string assetMessage = string.Empty;

            if (assetCount > 0)
            {
                assetMessage = $"(with {assetCount} asset{(assetCount is 1 ? string.Empty : "s")})";
            }

            return $"{path} {assetMessage}";
        }

        return path;
    }

    private enum ScanType
    {
        Overwrite,
        Append,
    }
}
