using APKognito.Configurations;
using APKognito.Models;
using APKognito.Models.Settings;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace APKognito.ViewModels.Pages;

public partial class DriveUsageViewModel : ObservableObject, IViewable
{
    private readonly KognitoConfig config;

    private List<FootprintInfo> cachedFootprints = [];

    #region Properties

    [ObservableProperty]
    private bool _isRunning =
#if DEBUG
        true;
#else
        false;
#endif

    [ObservableProperty]
    private Visibility _noFilesPanelVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private Visibility _fileListVisibility = Visibility.Visible;

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

    uint filter = 0;

    private bool _filterInRenamedApks;
    public bool FilterInRenamedApks
    {
        get => _filterInRenamedApks;
        set {
            if (value)
            {
                filter |= (uint)FootprintType.RenamedApk;
            }
            else
            {
                filter &= ~(uint)FootprintType.RenamedApk;
            }

            OnPropertyChanging(nameof(FilterInRenamedApks));
            _filterInRenamedApks = value;
            OnPropertyChanging(nameof(FilterInRenamedApks));

            UpdateItemsList();
        }
    }
    
    private bool _filterInDirectories;
    public bool FilterInDirectories
    {
        get => _filterInDirectories;
        set {
            uint flag = (uint)(FootprintType.Directory | FootprintType.TempDirectory);

            if (value)
            {
                filter |= flag;
            }
            else
            {
                filter &= ~flag;
            }
            
            OnPropertyChanging(nameof(FilterInDirectories));
            _filterInDirectories = value;
            OnPropertyChanged(nameof(FilterInDirectories));

            UpdateItemsList();
        }
    }
    
    private bool _filterInFiles;
    public bool FilterInFiles
    {
        get => _filterInFiles;
        set {
            uint flag = (uint)(FootprintType.File | FootprintType.TempFile);

            if (value)
            {
                filter |= flag;
            }
            else
            {
                filter &= ~flag;
            }

            OnPropertyChanging(nameof(FilterInFiles));
            _filterInFiles = value;
            OnPropertyChanged(nameof(FilterInFiles));

            UpdateItemsList();
        }
    }

    [ObservableProperty]
    private ObservableCollection<FootprintInfo> _foundFolders = [
#if DEBUG
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
#endif
        ];

    #endregion Properties

    public DriveUsageViewModel()
    {
        config = ConfigurationFactory.GetConfig<KognitoConfig>();

        FilterInRenamedApks = false;
        FilterInFiles = FilterInDirectories = true;
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

        foreach (FootprintInfo? item in itemsToDelete)
        {
            if (item.ItemType is FootprintType.File)
            {
                File.Delete(item.FolderPath);
            }
            else
            {
                Directory.Delete(item.FolderPath, true);
            }
        }

        // Get all item references that apply to the filter
        foreach (FootprintInfo? item in itemsToDelete)
        {
            _ = FoundFolders.Remove(item);
        }

        CanDelete = true;
        IsRunning = false;

        await StartSearch();
    }

    [RelayCommand]
    private async Task DeleteAllItems()
    {
        CanDelete = false;
        IsRunning = true;

        Wpf.Ui.Controls.MessageBox confirmation = new()
        {
            Title = $"Delete {GetFormattedItems()}?",
            Content = "All items displayed will be deleted (Click 'Cancel' to view once more if needed).\n\nContinue?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
        };

        MessageBoxResult result = await confirmation.ShowDialogAsync();

        if (result != MessageBoxResult.Primary)
        {
            goto Exit;
        }

        // Get all item references that apply to the filter
        foreach (FootprintInfo item in FoundFolders)
        {
            if (item.ItemType is FootprintType.File)
            {
                File.Delete(item.FolderPath);
            }
            else
            {
                Directory.Delete(item.FolderPath, true);
            }
        }

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
            foreach (var item in cachedFootprints.Where(fp => filter == 0 
                || ((FootprintType)filter).HasFlag(fp.ItemType)))
            {
                FoundFolders.Add(item);
                TotalFilteredSpace += item.FolderSizeBytes;
            }

            if (cachedFootprints.Count is 0)
            {
                NoFilesPanelVisibility = Visibility.Visible;
                FileListVisibility = Visibility.Collapsed;
                CanDelete = false;
            }
            else
            {
                NoFilesPanelVisibility = Visibility.Collapsed;
                FileListVisibility = Visibility.Visible;
                CanDelete = true;
            }
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
            // Not have AddRange is irritating
            cachedFootprints.Add(folderStat);
        }

        TotalUsedSpace = folderStats.Sum(f => f.FolderSizeBytes);
    }

    private string GetFormattedItems()
    {
        int folderCount = 0, fileCount = 0, apkCount = 0;

        foreach (var item in FoundFolders)
        {
            switch (item.ItemType)
            {
                case FootprintType.Directory:
                case FootprintType.TempDirectory:
                    folderCount++;
                    break;
                case FootprintType.File:
                case FootprintType.TempFile:
                    fileCount++;
                    break;
                case FootprintType.RenamedApk:
                    apkCount++;
                    break;
            }
        }

        StringBuilder sb = new StringBuilder();

        if (folderCount > 0)
        {
            sb.Append($"{folderCount} folder{(folderCount != 1 ? "s" : string.Empty)}, ");
        }

        if (fileCount > 0)
        {
            sb.Append($"{fileCount} file{(fileCount != 1 ? "s" : string.Empty)}, ");
        }

        if (apkCount > 0)
        {
            if (sb.Length > 0)
            {
                sb.Append("and ");
            }

            string plural = (apkCount != 1 ? "s" : string.Empty);
            sb.Append($"{apkCount} renamed APK{plural} and OBB{plural}");
        }
        else
        {
            // Trim the ", " from the last element
            return sb.ToString()[..^2];
        }

        return sb.ToString();
    }

    private static async Task<long> DirSizeAsync(DirectoryInfo d, CancellationToken cancellation)
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
}