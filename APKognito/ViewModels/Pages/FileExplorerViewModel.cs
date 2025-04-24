using APKognito.AdbTools;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Models;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using System.Collections.ObjectModel;
using System.IO;
using Wpf.Ui;

namespace APKognito.ViewModels.Pages;

public partial class FileExplorerViewModel : LoggableObservableObject
{
    private readonly AdbConfig adbConfig;

    private int directoryHistoryIndex = -1;

    private readonly List<string> directoryHistory = [];

    private string CurrentDirectory => directoryHistory[directoryHistoryIndex];

    #region Properties

    [ObservableProperty]
    public partial ObservableCollection<AdbFolderInfo> AdbItems { get; set; } = [];

    [ObservableProperty]
    public partial string ItemPath { get; set; } = "/";

    [ObservableProperty]
    public partial bool DirectoryEmpty { get; set; } = true;

    #endregion Properties

    public FileExplorerViewModel()
    {
        // For designer
#if DEBUG
        AdbItems.Add(AdbFolderInfo.DebugFiller);
        AdbItems.Add(AdbFolderInfo.DebugFiller);
        AdbItems.Add(AdbFolderInfo.DebugFiller);
        AdbItems.Add(AdbFolderInfo.DebugFiller);

        adbConfig = null!;
#endif
    }

    public FileExplorerViewModel(ISnackbarService _snackbarService, ConfigurationFactory _configFactory)
    {
        SetSnackbarProvider(_snackbarService);

        adbConfig = _configFactory.GetConfig<AdbConfig>();
    }

    #region Commands

    [RelayCommand]
    private async Task OnNavigateToDirectoryAsync(AdbFolderInfo info)
    {
        if (!await UpdateFoldersAsync(info))
        {
            return;
        }

        directoryHistoryIndex++;
        if (directoryHistoryIndex < directoryHistory.Count)
        {
            PruneHistory();
        }

        directoryHistory.Add(info.FullPath);
    }

    [RelayCommand]
    private async Task OnNavigateBackwardsAsync()
    {
        if (directoryHistoryIndex - 1 < 0)
        {
            return;
        }

        directoryHistoryIndex--;
        _ = await UpdateFoldersAsync(CurrentDirectory);
    }

    [RelayCommand]
    private async Task OnNavigateForwardsAsync()
    {
        if (directoryHistoryIndex + 1 >= directoryHistory.Count)
        {
            return;
        }

        directoryHistoryIndex++;
        _ = await UpdateFoldersAsync(CurrentDirectory);
    }

    [RelayCommand]
    private async Task OnNavigateOutOfDirectoryAsync()
    {
        string parentDirectoryPath = Path.GetDirectoryName(ItemPath)
            // Because god forbid someone want to do Unix path parsing while targeting Windows without
            // a pointless NuGet package or a custom class
            ?.Replace('\\', '/') ?? "/";

        if (!await UpdateFoldersAsync(parentDirectoryPath) && directoryHistory.Count > 0)
        {
            directoryHistory.Add(directoryHistory[^1]);
            directoryHistoryIndex++;
        }
    }

    [RelayCommand]
    private async Task OnTryRefreshDirectoryAsync()
    {
        _ = await UpdateFoldersAsync(directoryHistory[^1]);
    }

    #endregion Commands

    private async Task<bool> UpdateFoldersAsync(AdbFolderInfo? openingDirectory, bool silent = false)
    {
        // Start at the root device directory, change it to the parent directory
        // if openingDirectory isn't null
        string basePath = openingDirectory?.FullPath ?? string.Empty;

        return await UpdateFoldersAsync(basePath, silent);
    }

    private async Task<bool> UpdateFoldersAsync(string path, bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(adbConfig.CurrentDeviceId))
        {
            if (!silent)
            {
                SnackError(
                    "No device selected!",
                    "Cannot get directory or file information without a selected device."
                );
            }

            return false;
        }

        try
        {
            if (path.Length > 1)
            {
                path = path.TrimEnd('/');
            }

            AdbCommandOutput rawFileList = await AdbManager.QuickDeviceCommandAsync(
                // Redirect STDERR to null so filter out 'Permission denied' errors
                $"shell stat -c '{AdbFolderInfo.STAT_FORMAT_STRING}' {path}/* 2>/dev/null");

            string[] fileList = rawFileList.StdOut.Split("\r\n");

            IEnumerable<AdbFolderInfo> filteredFiles = fileList
                .Where(str => !string.IsNullOrWhiteSpace(str))
                .Select(str => new AdbFolderInfo(str, path));

            AdbItems.Clear();

            ItemPath = path;

            if (!filteredFiles.Any())
            {
                DirectoryEmpty = true;
                return false;
            }

            DirectoryEmpty = false;

            foreach (AdbFolderInfo item in filteredFiles)
            {
                AdbItems.Add(item);
            }

            return true;
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);

            string forItem = path is null
                ? "for root directory"
                : $"for {path}";

            if (!silent)
            {
                SnackError($"Failed to get directory descendants {forItem}", ex.Message);
            }
        }

        return false;
    }

    private void PruneHistory()
    {
        directoryHistory.RemoveRange(directoryHistoryIndex, directoryHistory.Count - directoryHistoryIndex);
    }
}
