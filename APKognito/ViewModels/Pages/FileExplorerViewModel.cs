using APKognito.AdbTools;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Models;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using Wpf.Ui;

namespace APKognito.ViewModels.Pages;

public partial class FileExplorerViewModel : LoggableObservableObject
{
    private readonly AdbConfig adbConfig = ConfigurationFactory.Instance.GetConfig<AdbConfig>();

    private int directoryHistoryIndex = -1;
    private readonly List<string> directoryHistory = [];
    private string CurrentDirectory => directoryHistory[directoryHistoryIndex];

    #region Properties

    [ObservableProperty]
    private double _viewHeight = 500;

    [ObservableProperty]
    private ObservableCollection<AdbFolderInfo> _adbItems = [];

    [ObservableProperty]
    private string _itemPath = "/";

    [ObservableProperty]
    private bool _directoryEmpty = true;

    #endregion Properties

    public FileExplorerViewModel()
    {
        // For designer

#if DEBUG
        _adbItems.Add(AdbFolderInfo.DebugFiller);
        _adbItems.Add(AdbFolderInfo.DebugFiller);
        _adbItems.Add(AdbFolderInfo.DebugFiller);
        _adbItems.Add(AdbFolderInfo.DebugFiller);
#endif
    }

    public FileExplorerViewModel(ISnackbarService _snackbarService)
    {
        SetSnackbarProvider(_snackbarService);
    }

    #region Commands

    [RelayCommand]
    private async Task OnNavigateToDirectory(AdbFolderInfo info)
    {
        if (await UpdateFolders(info))
        {
            directoryHistoryIndex++;

            if (directoryHistoryIndex < directoryHistory.Count)
            {
                PruneHistory();
            }

            directoryHistory.Add(info.FullPath);
        }
    }

    [RelayCommand]
    private async Task OnNavigateBackwards()
    {
        if (directoryHistoryIndex - 1 < 0)
        {
            return;
        }

        directoryHistoryIndex--;
        await UpdateFolders(CurrentDirectory);
    }

    [RelayCommand]
    private async Task OnNavigateForwards()
    {
        if (directoryHistoryIndex + 1 >= directoryHistory.Count)
        {
            return;
        }

        directoryHistoryIndex++;
        await UpdateFolders(CurrentDirectory);
    }

    [RelayCommand]
    private async Task OnNavigateOutOfDirectory()
    {
        string parentDirectoryPath = Path.GetDirectoryName(ItemPath)
            // Because god forbid someone want to do Unix path parsing while targeting Windows without
            // a pointless NuGet package or a custom class
            ?.Replace('\\', '/') ?? "/";

        if (!await UpdateFolders(parentDirectoryPath))
        {
            directoryHistory.Add(directoryHistory[^1]);
            directoryHistoryIndex++;
        }
    }

    [RelayCommand]
    private async Task OnTryRefreshDirectory()
    {
        await UpdateFolders(directoryHistory[^1]);
    }

    #endregion Commands

    private async Task<bool> UpdateFolders(AdbFolderInfo? openingDirectory, bool silent = false)
    {
        // Start at the root device directory, change it to the parent directory
        // if openingDirectory isn't null
        string basePath = openingDirectory?.FullPath ?? string.Empty;

        return await UpdateFolders(basePath, silent);
    }

    private async Task<bool> UpdateFolders(string path, bool silent = false)
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

            string[] response = (await AdbManager.QuickDeviceCommand(
                // Redirect STDERR to null so filter out 'Permission denied' errors
                $"shell stat -c '{AdbFolderInfo.FormatString}' {path}/* 2>/dev/null")).StdOut
                .Split("\r\n");

            var filtered = response
                .Where(str => !string.IsNullOrWhiteSpace(str))
                .Select(str => new AdbFolderInfo(str, path));

            AdbFolderInfo[] newItems = [.. filtered];

            await Dispatcher.CurrentDispatcher.InvokeAsync(() =>
            {
                AdbItems.Clear();

                ItemPath = path;

                if (newItems.Length is 0)
                {
                    DirectoryEmpty = true;
                    return;
                }

                DirectoryEmpty = false;

                foreach (var item in newItems)
                {
                    AdbItems.Add(item);
                }
            });

            return true;
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);

            string forItem = path is null
                ? " for root directory"
                : $" for {path}";

            if (!silent)
            {
                SnackError($"Failed to get directory descendants{forItem}", ex.Message);
            }
        }

        return false;
    }

    private void PruneHistory()
    {
        directoryHistory.RemoveRange(directoryHistoryIndex, directoryHistory.Count - directoryHistoryIndex);
    }
}
