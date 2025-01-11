using APKognito.AdbTools;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Models;
using APKognito.Utilities;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using Wpf.Ui;

namespace APKognito.ViewModels.Pages;

public partial class FileExplorerViewModel : LoggableObservableObject, IViewable
{
    private readonly AdbConfig adbConfig = ConfigurationFactory.GetConfig<AdbConfig>();

    private int directoryHistoryIndex = -1;
    private readonly List<AdbFolderInfo> directoryHistory = [];

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
                directoryHistory.RemoveRange(directoryHistoryIndex, directoryHistory.Count - directoryHistoryIndex);
            }

            directoryHistory.Add(info);
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
        await UpdateFolders(directoryHistory[directoryHistoryIndex]);
    }

    [RelayCommand]
    private async Task OnNavigateOutOfDirectory()
    {
        if (directoryHistoryIndex - 1 < 0)
        {
            return;
        }

        directoryHistoryIndex--;
        AdbFolderInfo? parentHistoryDirectory = directoryHistory[directoryHistoryIndex];
        directoryHistory.RemoveAt(directoryHistoryIndex);

        string parentDirectoryPath = Path.GetDirectoryName(ItemPath)?.Replace('\\', '/') ?? "/";

        if (!await UpdateFolders(null, parentDirectoryPath))
        {
            directoryHistory.Add(parentHistoryDirectory);
            directoryHistoryIndex++;
        }
    }

    [RelayCommand]
    private async Task OnTryRefreshDirectory()
    {
        await UpdateFolders(directoryHistory[^1]);
    }

    #endregion Commands

    /// <summary>
    /// Returns <see langword="true"/> when the folder list has been updated and rendered, thus switching directories. Otherwise <see langword="false"/>.
    /// </summary>
    /// <param name="openingDirectory"></param>
    /// <param name="overridePath"></param>
    /// <param name="silent"></param>
    /// <returns></returns>
    private async Task<bool> UpdateFolders(AdbFolderInfo? openingDirectory, string? overridePath = null, bool silent = false)
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
            // Start at the root device directory, change it to the parent directory
            // if openingDirectory isn't null
            string basePath = openingDirectory?.FullPath
                ?? overridePath
                ?? string.Empty;

            string[] response = (await AdbManager.QuickDeviceCommand(
                // Redirect STDERR to null so filter out 'Permission denied' errors
                $"shell stat -c '{AdbFolderInfo.FormatString}' {basePath}/* 2>/dev/null")).StdOut
                .Split("\r\n");

            var filtered = response
                .Where(str => !string.IsNullOrWhiteSpace(str))
                .Select(str => new AdbFolderInfo(str, openingDirectory));

            AdbFolderInfo[] newItems = [.. filtered];

            await Dispatcher.CurrentDispatcher.InvokeAsync(() =>
            {
                AdbItems.Clear();

                ItemPath = basePath;

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

            string forItem = openingDirectory is null
                ? " for root directory"
                : $" for {openingDirectory.FileName}";

            if (!silent)
            {
                SnackError($"Failed to get directory descendants{forItem}", ex.Message);
            }
        }

        return false;
    }
}
