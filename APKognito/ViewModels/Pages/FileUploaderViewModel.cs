using System.Collections.ObjectModel;
using System.IO;
using APKognito.AdbTools;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using Microsoft.Win32;
using Wpf.Ui;

namespace APKognito.ViewModels.Pages;

public partial class FileUploaderViewModel : LoggableObservableObject
{
    private readonly AdbConfig adbConfig;

    private readonly List<string> _files = [];

    #region Properties

    [ObservableProperty]
    public partial ObservableCollection<ListBoxItem> SelectedItems { get; set; } = [];

    [ObservableProperty]
    public partial bool IsUploading { get; set; } = false;

    #endregion Properties

    public FileUploaderViewModel(
        ConfigurationFactory configFactory,
        ISnackbarService snackbarService
    ) : base(configFactory)
    {
        SetSnackbarProvider(snackbarService);

        adbConfig = configFactory.GetConfig<AdbConfig>();
    }

    public FileUploaderViewModel()
    {
        // For designer
        adbConfig = null!;
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
    private async Task OnAddRecurseItemsAsync()
    {
        await PromptForRecurseFilesAsync();
    }

    [RelayCommand]
    private async Task OnUploadItemsToDeviceAsync()
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
                _ = await AdbManager.QuickDeviceCommandAsync(@$"install -g ""{path}""");
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
                    AdbCommandOutput result = await AdbManager.QuickDeviceCommandAsync($"push {assets} /sdcard/Android/obb/");
                    FileLogger.Log(result.StdOut);
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

    private async Task PromptForRecurseFilesAsync()
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

        return Directory.Exists(obbDirectory) ? obbDirectory : null;
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
