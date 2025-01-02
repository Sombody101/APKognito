using APKognito.Configurations;
using APKognito.Models;
using APKognito.Models.Settings;
using APKognito.Utilities;
using Humanizer;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Threading;

namespace APKognito.ViewModels.Pages;

public partial class DriveUsageViewModel : PageSizeTracker, IViewable
{
    private readonly KognitoConfig config;

    private readonly List<FootprintInfo> cachedFootprints = [];

    private uint filter = 0;

    #region Properties

    [ObservableProperty]
    private double _listHeight = 500;

    [ObservableProperty]
    private bool _isRunning =
#if DEBUG
        true;
#else
        false;
#endif

    [ObservableProperty]
    private bool _fileListVisibility = true;

    [ObservableProperty]
    private string _startButtonText = "Refresh";

    [ObservableProperty]
    private long _totalUsedSpace = 0;

    [ObservableProperty]
    private long _totalFilteredSpace = 0;

    [ObservableProperty]
    private long _totalSelectedSpace = 0;

    [ObservableProperty]
    private bool _canDelete = false;

    [ObservableProperty]
    private bool _canModifyFilter = false;

    [ObservableProperty]
    private bool _filterInRenamedApks;

    [ObservableProperty]
    private bool _filterInDirectories;

    [ObservableProperty]
    private bool _filterInFiles;

    [ObservableProperty]
    private ObservableCollection<FootprintInfo> _foundFolders = [];

    #endregion Properties

    public DriveUsageViewModel()
    {
        config = ConfigurationFactory.GetConfig<KognitoConfig>();

        FilterInRenamedApks = false;
        FilterInFiles = FilterInDirectories = true;

        WindowSizeChanged += (sender, e) =>
        {
            ListHeight = WindowHeight - TitlebarHeight - 105;
        };

#if DEBUG
        PopulateList();
#endif
    }

    #region Commands

    private CancellationTokenSource? _collectDataCancelationSource;

    [RelayCommand]
    public async Task StartSearch()
    {
        if (IsRunning)
        {
            StartButtonText = "Refresh";
            IsRunning = false;

            _ = (_collectDataCancelationSource?.CancelAsync());
        }
        else
        {
            StartButtonText = "Cancel";
            IsRunning = true;

            cachedFootprints.Clear();
            TotalUsedSpace = 0;
            _collectDataCancelationSource ??= new();

            try
            {
                await CollectDiskUsage(_collectDataCancelationSource.Token);
                UpdateItemsList();
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation
            }
            finally
            {
                _collectDataCancelationSource.Dispose();
                _collectDataCancelationSource = null;
                StartButtonText = "Refresh";
                IsRunning = false;
            }
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedItems(ListView folderList)
    {
        CanDelete = false;
        IsRunning = true;

        List<FootprintInfo> itemsToDelete = folderList.SelectedItems.Cast<FootprintInfo>().ToList();

        if (itemsToDelete.Count < 1)
        {
            goto Exit;
        }

        MessageBox confirmation = new()
        {
            Title = $"Delete {GetFormattedItems(itemsToDelete)}?",
            Content = "All previously selected items will be deleted. (Click 'Cancel' to view once more if needed).\n\nContinue?",
            PrimaryButtonText = "Delete",
            PrimaryButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger,
            CloseButtonText = "Cancel",
        };

        MessageBoxResult result = await confirmation.ShowDialogAsync();

        if (result != MessageBoxResult.Primary)
        {
            goto Exit;
        }

        await DeleteFileCollection(itemsToDelete);
        FoundFolders.Clear();

    Exit:
        CanDelete = true;
        IsRunning = false;

        await StartSearch();
    }

    [RelayCommand]
    private async Task DeleteAllItems()
    {
        CanDelete = false;
        IsRunning = true;

        if (cachedFootprints.Count < 1)
        {
            goto Exit;
        }

        MessageBox confirmation = new()
        {
            Title = $"Delete {GetFormattedItems(FoundFolders)}?",
            Content = "All items displayed will be deleted (Click 'Cancel' to view once more if needed).\n\nContinue?",
            PrimaryButtonText = "Delete",
            PrimaryButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger,
            CloseButtonText = "Cancel",
        };

        MessageBoxResult result = await confirmation.ShowDialogAsync();

        if (result != MessageBoxResult.Primary)
        {
            goto Exit;
        }

        await DeleteFileCollection(FoundFolders);
        FoundFolders.Clear();

    Exit:
        CanDelete = true;
        IsRunning = false;

        await StartSearch();
    }

    #endregion Commands

    public void UpdateItemsList()
    {
        Dispatcher.CurrentDispatcher.Invoke(() =>
        {
            FoundFolders.Clear();
            TotalFilteredSpace = 0;

            if (cachedFootprints.Count is 0)
            {
                return;
            }

            // The filter is just a generic mask. Each item checks if the flag is set within the filter.
            foreach (FootprintInfo? item in cachedFootprints.Where(fp => filter == 0
                || ((FootprintTypes)filter).HasFlag(fp.ItemType)))
            {
                FoundFolders.Add(item);
                TotalFilteredSpace += item.FolderSizeBytes;
            }

            CanDelete = FileListVisibility = cachedFootprints.Count is not 0;

            // if (cachedFootprints.Count is 0)
            // {
            //     NoFilesPanelVisibility = Visibility.Visible;
            //     FileListVisibility = Visibility.Collapsed;
            //     CanDelete = false;
            // }
            // else
            // {
            //     NoFilesPanelVisibility = Visibility.Collapsed;
            //     FileListVisibility = Visibility.Visible;
            //     CanDelete = true;
            // }
        });
    }

    public async Task CollectDiskUsage(CancellationToken cancellation)
    {
        List<string> folders = [];
        folders.AddRange(Directory.GetDirectories(Path.GetTempPath(), "APKognito-*"));

        string apkOutputPath = config.ApkOutputDirectory ?? string.Empty;
        if (Directory.Exists(apkOutputPath))
        {
            apkOutputPath = Path.GetFullPath(apkOutputPath);
            folders.AddRange(Directory.GetDirectories(apkOutputPath));
            folders.AddRange(Directory.GetFiles(apkOutputPath));
        }

        List<Task<FootprintInfo>> tasks = [];
        foreach (string folderName in folders)
        {
            if (cancellation.IsCancellationRequested)
            {
                break;
            }

            tasks.Add(Task.Run(async () =>
            {
                FileAttributes attrs = await Task.Run(() => File.GetAttributes(folderName), cancellation);
                if (attrs.HasFlag(FileAttributes.Directory))
                {
                    DirectoryInfo di = new(folderName);
                    long size = await DirSizeAsync(di, cancellation);
                    return new FootprintInfo(di, size);
                }
                else
                {
                    FileInfo fi = new(folderName);
                    return new FootprintInfo(fi);
                }
            }, cancellation));
        }

        FootprintInfo[] folderStats = await Task.WhenAll(tasks);

        foreach (FootprintInfo? folderStat in folderStats)
        {
            // Not having AddRange is irritating
            cachedFootprints.Add(folderStat);
        }

        TotalUsedSpace = folderStats.Sum(f => f.FolderSizeBytes);
    }

    public static async Task<long> DirSizeAsync(DirectoryInfo d, CancellationToken cancellation)
    {
        List<Task<long>> tasks = [];
        foreach (FileInfo fi in d.GetFiles())
        {
            tasks.Add(Task.Run(() => fi.Length, cancellation));
        }

        foreach (DirectoryInfo di in d.GetDirectories())
        {
            tasks.Add(DirSizeAsync(di, cancellation));
        }

        _ = await Task.WhenAll(tasks);
        long[] results = await Task.WhenAll(tasks);

        return results.Sum();
    }

    partial void OnFilterInRenamedApksChanged(bool value)
    {
        if (value)
        {
            filter |= (uint)FootprintTypes.RenamedApk;
        }
        else
        {
            filter &= ~(uint)FootprintTypes.RenamedApk;
        }

        UpdateItemsList();
    }

    partial void OnFilterInDirectoriesChanged(bool value)
    {
        const uint flag = (uint)(FootprintTypes.Directory | FootprintTypes.TempDirectory);

        if (value)
        {
            filter |= flag;
        }
        else
        {
            filter &= ~flag;
        }

        UpdateItemsList();
    }

    partial void OnFilterInFilesChanged(bool value)
    {
        const uint flag = (uint)(FootprintTypes.File | FootprintTypes.TempFile);

        if (value)
        {
            filter |= flag;
        }
        else
        {
            filter &= ~flag;
        }

        UpdateItemsList();
    }

    private static async ValueTask DeleteFileCollection(IEnumerable<FootprintInfo> files)
    {
        await Parallel.ForEachAsync(files, (item, token) =>
        {
            if (item.ItemType is FootprintTypes.File)
            {
                File.Delete(item.FolderPath);
            }
            else
            {
                Directory.Delete(item.FolderPath, true);
            }

            return ValueTask.CompletedTask;
        });
    }

    private static string GetFormattedItems(IEnumerable<FootprintInfo> list)
    {
        if (!list.Any())
        {
            return string.Empty;
        }

        int folderCount = 0, fileCount = 0, apkCount = 0;

        foreach (FootprintInfo item in list)
        {
            switch (item.ItemType)
            {
                case FootprintTypes.Directory:
                case FootprintTypes.TempDirectory:
                    folderCount++;
                    break;

                case FootprintTypes.File:
                case FootprintTypes.TempFile:
                    fileCount++;
                    break;

                case FootprintTypes.RenamedApk:
                    apkCount++;
                    break;
            }
        }

        StringBuilder sb = new();

        if (folderCount > 0)
        {
            _ = sb.Append($"{folderCount} {"folder".PluralizeIfMany(folderCount)}, ");
        }

        if (fileCount > 0)
        {
            _ = sb.Append($"{fileCount} {"file".PluralizeIfMany(fileCount)}, ");
        }

        if (apkCount > 0)
        {
            if (sb.Length > 0)
            {
                _ = sb.Append("and ");
            }

            string plural = apkCount != 1
                ? "s"
                : string.Empty;

            _ = sb.Append($"{apkCount} renamed APK{plural} and OBB{plural}");
        }
        else
        {
            // Trim the ", " from the last element
            return sb.ToString()[..^2];
        }

        return sb.ToString();
    }

    #region DEBUG_ONLY

#if DEBUG

    public void PopulateList()
    {
        FoundFolders = [
            new("C:\\Windows\\Help\\APKognito.2355.temp", 27349872928),
            new("C:\\Windows\\System32\\APKognito.6745f.temp", 8388392),
            new("C:\\Windows\\SysWow6432\\APKognito.35422.temp", 2992),
            new("C:\\Windows\\System32\\APKognito.3847958.temp", 234095728),
            new("C:\\Windows\\Help\\APKognito.2355.temp", 27349872928),
            new("C:\\Windows\\System32\\APKognito.6745f.temp", 8388392),
            new("C:\\Windows\\SysWow6432\\APKognito.35422.temp", 2992),
            new("C:\\Windows\\System32\\APKognito.3847958.temp", 234095728),
            new("C:\\Windows\\Help\\APKognito.2355.temp", 27349872928),
            new("C:\\Windows\\System32\\APKognito.6745f.temp", 8388392),
            new("C:\\Windows\\SysWow6432\\APKognito.35422.temp", 2992),
            new("C:\\Windows\\System32\\APKognito.3847958.temp", 234095728),
            new("C:\\Windows\\Help\\APKognito.2355.temp", 27349872928),
            new("C:\\Windows\\System32\\APKognito.6745f.temp", 8388392),
            new("C:\\Windows\\SysWow6432\\APKognito.35422.temp", 2992),
            new("C:\\Windows\\System32\\APKognito.3847958.temp", 234095728),
            new("C:\\Windows\\Help\\APKognito.2355.temp", 27349872928),
            new("C:\\Windows\\System32\\APKognito.6745f.temp", 8388392),
            new("C:\\Windows\\SysWow6432\\APKognito.35422.temp", 2992),
            new("C:\\Windows\\System32\\APKognito.3847958.temp", 234095728),
        ];
    }

#endif

    #endregion DEBUG_ONLY
}