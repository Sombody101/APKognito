using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using APKognito.AdbTools;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Controls;
using APKognito.Controls.Dialogs;
using APKognito.Models;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using Wpf.Ui;

namespace APKognito.ViewModels.Pages;

public partial class FileExplorerViewModel : LoggableObservableObject
{
    private readonly AdbConfig _adbConfig;
    private readonly IContentDialogService _dialogService;

    private int _directoryHistoryIndex = -1;

    private readonly List<string> _directoryHistory = [];

    private string CurrentDirectory => _directoryHistory[_directoryHistoryIndex];

    #region Properties

    [ObservableProperty]
    public partial ObservableCollection<AdbFileEntry> AdbItems { get; set; } = [];

    [ObservableProperty]
    public partial string ItemPath { get; set; } = "/";

    [ObservableProperty]
    public partial bool DirectoryEmpty { get; set; } = true;

    #endregion Properties

#if DEBUG
    public FileExplorerViewModel()
    {
        // For designer
        AdbItems.Add(AdbFileEntry.DebugFiller);
        AdbItems.Add(AdbFileEntry.DebugFiller);
        AdbItems.Add(AdbFileEntry.DebugFiller);
        AdbItems.Add(AdbFileEntry.DebugFiller);

        _adbConfig = null!;
        _dialogService = null!;
        DirectoryEmpty = false;
    }
#endif

    public FileExplorerViewModel(
        ConfigurationFactory configFactory,
        ISnackbarService snackbarService,
        IContentDialogService dialogService
    ) : base(configFactory)
    {
        SetSnackbarProvider(snackbarService);

        _adbConfig = configFactory.GetConfig<AdbConfig>();
        _dialogService = dialogService;
    }

    #region Commands

    [RelayCommand]
    private async Task OnNavigateToDirectoryAsync(AdbFileEntry info)
    {
        if (info is null)
        {
            return;
        }

        if (!await UpdateFoldersAsync(info))
        {
            return;
        }

        _directoryHistoryIndex++;
        if (_directoryHistoryIndex < _directoryHistory.Count)
        {
            PruneHistory();
        }

        _directoryHistory.Add(info.FullPath);
    }

    [RelayCommand]
    private async Task OnNavigateBackwardsAsync()
    {
        if (_directoryHistoryIndex - 1 < 0)
        {
            return;
        }

        _directoryHistoryIndex--;
        _ = await UpdateFoldersAsync(CurrentDirectory);
    }

    [RelayCommand]
    private async Task OnNavigateForwardsAsync()
    {
        if (_directoryHistoryIndex + 1 >= _directoryHistory.Count)
        {
            return;
        }

        _directoryHistoryIndex++;
        _ = await UpdateFoldersAsync(CurrentDirectory);
    }

    [RelayCommand]
    private async Task OnNavigateOutOfDirectoryAsync()
    {
        string parentDirectoryPath = Path.GetDirectoryName(ItemPath)
            // Because god forbid someone want to use a Unix path while targeting Windows without
            // a wasteful NuGet package or a custom class
            ?.Replace('\\', '/') ?? "/";

        if (!await UpdateFoldersAsync(parentDirectoryPath) && _directoryHistory.Count > 0)
        {
            _directoryHistory.Add(_directoryHistory[^1]);
            _directoryHistoryIndex++;
        }
        else if (_directoryHistory.Count > 0 && _directoryHistory[^1] != parentDirectoryPath)
        {
            _directoryHistory.Add(parentDirectoryPath);
            ++_directoryHistoryIndex;
        }
    }

    [RelayCommand]
    private async Task OnTryRefreshDirectoryAsync()
    {
        _ = await UpdateFoldersAsync(_directoryHistory[^1]);
    }

    [RelayCommand]
    private async Task OnManualNavigateAsync(string path)
    {
        await NavigateArbitraryAsync(path);
    }

    /*
     * Menu item buttons
     */

    [RelayCommand]
    private async Task OnPullItemsAsync(ICollection viewEntries)
    {
        IEnumerable<AdbFileEntry> entries = viewEntries.Cast<AdbFileEntry>();

        string? directory = DirectorySelector.UserSelectDirectory();

        if (directory is null)
        {
            return;
        }

        ConsoleDialog consoleDialog = new(_dialogService.GetDialogHost());
        Task<WPFUI.Controls.ContentDialogResult> resultTask = consoleDialog.ShowAsync();

        try
        {
            foreach (AdbFileEntry entry in entries)
            {
                string itemPath = entry.FullPath;

                if (itemPath.Contains('"'))
                {
                    consoleDialog.ViewModel.LogError($"Unable to pull '{itemPath}'. Invalid path.");
                    continue;
                }

                consoleDialog.ViewModel.Log($"Pulling: {itemPath}");
                await consoleDialog.RunAdbCommandAsync($"pull \"{itemPath}\" \"{directory}\"");
            }
        }
        catch (Exception ex)
        {
            consoleDialog.ViewModel.LogError($"Failed to pull item: {ex.Message}");
            consoleDialog.ViewModel.LogDebug(ex);
            FileLogger.LogException(ex);
        }

        consoleDialog.Finished();
        _ = await resultTask;
    }

    #endregion Commands

    private async Task NavigateArbitraryAsync(string path)
    {
        if (await UpdateFoldersAsync(path))
        {
            ++_directoryHistoryIndex;
            _directoryHistory.Add(path);
        }
    }

    private async Task<bool> UpdateFoldersAsync(AdbFileEntry? openingDirectory, bool silent = false)
    {
        // Start at the root device directory, change it to the parent directory
        // if openingDirectory isn't null
        string basePath = openingDirectory?.FullPath ?? string.Empty;

        return await UpdateFoldersAsync(basePath, silent);
    }

    private async Task<bool> UpdateFoldersAsync(string path, bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(_adbConfig.CurrentDeviceId))
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
                $"shell stat -c '{AdbFileEntry.STAT_FORMAT_STRING}' {path}/* 2>/dev/null");

            string[] fileList = rawFileList.StdOut.Split("\r\n");

            IEnumerable<AdbFileEntry> filteredFiles = fileList
                .Where(str => !string.IsNullOrWhiteSpace(str))
                .Select(str => new AdbFileEntry(str, path));

            AdbItems.Clear();

            ItemPath = path;

            if (!filteredFiles.Any())
            {
                DirectoryEmpty = true;
                return true;
            }

            DirectoryEmpty = false;

            foreach (AdbFileEntry item in filteredFiles)
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
        _directoryHistory.RemoveRange(_directoryHistoryIndex, _directoryHistory.Count - _directoryHistoryIndex);
    }
}
