using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Utilities;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Controls;
using Wpf.Ui;

namespace APKognito.ViewModels.Pages;

public partial class FileUploaderViewModel : LoggableObservableObject, IViewable
{
    private readonly AdbConfig adbConfig = ConfigurationFactory.GetConfig<AdbConfig>();

    private readonly List<string> _files = [];

    #region Properties

    [ObservableProperty]
    private ObservableCollection<ListBoxItem> selectedItems = [];

    [ObservableProperty]
    private bool isUploading = false;

    #endregion Properties

    public FileUploaderViewModel(ISnackbarService _snackbarService)
    {
        SetSnackbarProvider(_snackbarService);
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
            SnackError(
                 "No device selected",
                "No ADB device has been selected. Go to the ADB Configuration page and choose a device."
            );

            return;
        }

        if (_files.Count is 0)
        {
            SnackError(
                 "No files selected",
                "No files have been selected to upload."
            );

            return;
        }

        IsUploading = true;

        AdbDeviceInfo? adbPaths = adbConfig.GetCurrentDevice();

        if (adbPaths is null)
        {
            SnackError("No ADB device selected", "No ADB device is selected");
            return;
        }

        foreach (string path in _files)
        {
            try
            {
                await AdbManager.QuickDeviceCommand(@$"install -g ""{path}""");
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex);
                SnackError($"Failed to install {Path.GetFileName(path)}", ex.Message);
            }

            FileLogger.Log($"Pushing {_files.Count} files to {adbConfig.CurrentDeviceId}");

            string? assets = GetApkAssetsDirectory(path);
            if (assets is not null)
            {
                try
                {
                    string output = await AdbManager.QuickDeviceCommand($"push {assets} {adbPaths.InstallPaths.ObbPath}");
                    FileLogger.Log(output);
                }
                catch (Exception ex)
                {
                    FileLogger.LogException(ex);
                    SnackError($"Failed to upload {Path.GetDirectoryName(assets)}", ex.Message);
                }
            }
        }

        SnackSuccess("Upload complete", string.Empty);

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
                SnackWarning(
                    $"{duplicatePaths} duplicate path{(duplicatePaths is 1 ? string.Empty : "s")} left out",
                    $"{duplicatePaths}/{openFileDialog.FileNames.Length} paths were not added to the list as they were already in the list."
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
        string obbDirectory = Path.Combine(Path.GetDirectoryName(path) ?? path, Path.GetFileNameWithoutExtension(path));

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
