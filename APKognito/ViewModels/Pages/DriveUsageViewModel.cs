using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Documents;
using System.Windows.Threading;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Helpers;
using APKognito.Models;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using Wpf.Ui.Controls;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace APKognito.ViewModels.Pages;

public partial class DriveUsageViewModel : ViewModel, IViewable
{
    private const string PACKAGE_LIST_JOIN_STRING = "\n  ⚬  ";

    private const string TEXT_REFRESH = "Refresh",
        TEXT_CANCEL = "Cancel";

    private readonly UserRenameConfiguration _kognitoConfig;

    private readonly List<FootprintInfo> _cachedFootprints = [];

    private uint _filter = 0;

    #region Properties

    [ObservableProperty]
    public partial bool IsRunning { get; set; } =
#if DEBUG
        true;
#else
        false;
#endif

    [ObservableProperty]
    public partial bool FileListVisibility { get; set; } = true;

    [ObservableProperty]
    public partial string StartButtonText { get; set; } = TEXT_REFRESH;

    [ObservableProperty]
    public partial ulong TotalUsedSpace { get; set; } = 0;

    [ObservableProperty]
    public partial ulong TotalFilteredSpace { get; set; } = 0;

    [ObservableProperty]
    public partial ulong TotalSelectedSpace { get; set; } = 0;

    [ObservableProperty]
    public partial bool CanDelete { get; set; } = false;

    [ObservableProperty]
    public partial bool CanModifyFilter { get; set; } = false;

    [ObservableProperty]
    public partial bool FilterInRenamedApks { get; set; }

    [ObservableProperty]
    public partial bool FilterInDirectories { get; set; }

    [ObservableProperty]
    public partial bool FilterInFiles { get; set; }

    [ObservableProperty]
    public partial string CurrentlyDeleting { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CurrentlyDeletingLow { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<FootprintInfo> FoundFolders { get; set; } = [];

    #endregion Properties

    public DriveUsageViewModel(ConfigurationFactory _configFactory)
    {
        _kognitoConfig = _configFactory.GetConfig<UserRenameConfiguration>();

        FilterInRenamedApks = false;
        FilterInFiles = FilterInDirectories = true;

#if DEBUG
        PopulateList();
#endif
    }

#if DEBUG
    public DriveUsageViewModel()
    {
        // For designer
        _kognitoConfig = null!;
    }
#endif

    #region Commands

    private CancellationTokenSource? _collectDataCancelationSource;

    [RelayCommand]
    public async Task StartSearchAsync()
    {
        if (IsRunning)
        {
            StartButtonText = TEXT_REFRESH;
            IsRunning = false;

            _ = (_collectDataCancelationSource?.CancelAsync());
        }
        else
        {
            StartButtonText = TEXT_CANCEL;
            IsRunning = true;

            _cachedFootprints.Clear();
            TotalUsedSpace = 0;
            _collectDataCancelationSource ??= new();

            try
            {
                await CollectDiskUsageAsync(_collectDataCancelationSource.Token);
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
    private async Task DeleteSelectedItemsAsync(ListView folderList)
    {
        CanDelete = false;
        IsRunning = true;

        List<FootprintInfo> itemsToDelete = folderList.SelectedItems.Cast<FootprintInfo>().ToList();

        if (itemsToDelete.Count > 0 && await PromptForDeletionAsync(itemsToDelete))
        {
            await DeleteFileCollectionAsync(itemsToDelete);
            FoundFolders.Clear();
        }

        CanDelete = true;
        IsRunning = false;
        CurrentlyDeleting = CurrentlyDeletingLow = string.Empty;

        await StartSearchAsync();
    }

    [RelayCommand]
    private async Task DeleteAllItemsAsync()
    {
        CanDelete = false;
        IsRunning = true;

        if (_cachedFootprints.Count > 0 && await PromptForDeletionAsync(FoundFolders))
        {
            await DeleteFileCollectionAsync(FoundFolders);
            FoundFolders.Clear();
        }

        CanDelete = true;
        IsRunning = false;
        CurrentlyDeleting = CurrentlyDeletingLow = string.Empty;

        await StartSearchAsync();
    }

    #endregion Commands

    public override async Task OnNavigatedToAsync()
    {
        await StartSearchAsync();
    }

    public void UpdateItemsList()
    {
        Dispatcher.CurrentDispatcher.Invoke(() =>
        {
            FoundFolders.Clear();
            TotalFilteredSpace = 0;

            if (_cachedFootprints.Count is 0)
            {
                CanDelete = FileListVisibility = false;
                return;
            }

            // The filter is just a generic mask. Each item checks if the flag is set within the filter.
            foreach (FootprintInfo? item in _cachedFootprints.Where(fp => _filter == 0
                || ((FootprintTypes)_filter).HasFlag(fp.ItemType)))
            {
                FoundFolders.Add(item);
                TotalFilteredSpace += item.FolderSizeBytes;
            }

            CanDelete = FileListVisibility = FoundFolders.Count is not 0;
        });
    }

    public async Task CollectDiskUsageAsync(CancellationToken cancellation)
    {
        List<string> folders = [];

        // Temp directories
        folders.AddRange(Directory.GetDirectories(Path.GetTempPath(), "APKognito-*"));

        // Renamed apps
        string apkOutputPath = _kognitoConfig.ApkOutputDirectory ?? string.Empty;
        if (Directory.Exists(apkOutputPath))
        {
            apkOutputPath = Path.GetFullPath(apkOutputPath);
            folders.AddRange(Directory.GetDirectories(apkOutputPath).Where(path => DirectoryManager.IsDirectoryClaimed(path)));
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
                FileAttributes attributes = await Task.Run(() => File.GetAttributes(folderName), cancellation);
                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    DirectoryInfo di = new(folderName);
                    ulong size = await DirectoryManager.GetDirectorySizeAsync(di, cancellation);
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
            _cachedFootprints.Add(folderStat);
        }

        TotalUsedSpace = folderStats.Length > 0
            ? folderStats.Select(f => f.FolderSizeBytes).Aggregate((a, c) => a + c)
            : 0;
    }

    partial void OnFilterInRenamedApksChanged(bool value)
    {
        UpdateFilterFlag(value, FootprintTypes.RenamedApk);
        UpdateItemsList();
    }

    partial void OnFilterInDirectoriesChanged(bool value)
    {
        UpdateFilterFlag(value, FootprintTypes.Directory | FootprintTypes.TempDirectory);
        UpdateItemsList();
    }

    partial void OnFilterInFilesChanged(bool value)
    {
        UpdateFilterFlag(value, FootprintTypes.File | FootprintTypes.TempFile);
        UpdateItemsList();
    }

    private static string GetFormattedSelectedPackages(IEnumerable<FootprintInfo> items, string joinStr = PACKAGE_LIST_JOIN_STRING)
    {
        return $"{joinStr}{string.Join(joinStr, items.Select(item => item.FolderName))}";
    }

    private async Task DeleteFileCollectionAsync(IEnumerable<FootprintInfo> files)
    {
        // Horrible to the thread pool, but I'm not sure what else to do right now...
        // We can only hope the user doesn't run a deletion while renaming a package.
        await Task.Run(() =>
        {
            foreach (FootprintInfo entry in files)
            {
                CurrentlyDeleting = $"{Path.GetFileName(entry.FolderName)} ({GBConverter.FormatSizeFromBytes(entry.FolderSizeBytes)})";

                if (entry.ItemType is FootprintTypes.File)
                {
                    CurrentlyDeleting = Path.GetFileName(entry.FolderPath);
                    File.Delete(entry.FolderPath);
                }
                else
                {
                    DeleteDirectory(entry.FolderPath);
                }
            }
        });

        void DeleteDirectory(string directory)
        {
            foreach (string file in Directory.EnumerateFileSystemEntries(directory))
            {
                try
                {
                    CurrentlyDeletingLow = Path.GetFileName(file);
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                    else
                    {
                        DeleteDirectory(file);
                    }
                }
                catch
                {
                    // Skip
                    CurrentlyDeletingLow = "Failed to delete some files!";
                }
            }

            Directory.Delete(directory);
        }
    }

    private static async Task<bool> PromptForDeletionAsync(IEnumerable<FootprintInfo> itemsToDelete)
    {
        MessageBox confirmation = new()
        {
            Title = $"Delete {GetFormattedItems(itemsToDelete)}?",
            Content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Inlines =
                {
                    new Run("This will remove the following items:\n")
                    {
                        FontWeight = FontWeights.Bold
                    },
                    new ScrollViewer
                    {
                        MaxHeight = 450,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        Content = new TextBlock
                        {
                            Text = GetFormattedSelectedPackages(itemsToDelete),
                            TextWrapping = TextWrapping.Wrap
                        }
                    },
                    new LineBreak(),
                    new LineBreak(),
                    new Run("Continue?") { FontWeight = FontWeights.Bold }
                }
            },
            PrimaryButtonText = "Delete",
            PrimaryButtonAppearance = ControlAppearance.Danger,
            CloseButtonText = "Cancel",
            MinWidth = 450,
            MaxHeight = 600,
        };

        MessageBoxResult result = await confirmation.ShowDialogAsync();
        return result is MessageBoxResult.Primary;
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

    private void UpdateFilterFlag(bool value, FootprintTypes flag)
    {
        uint flagUint = (uint)flag;

        if (value)
        {
            _filter |= flagUint;
        }
        else
        {
            _filter &= ~flagUint;
        }

        UpdateItemsList();
    }

    #region DEBUG_ONLY

#if DEBUG

    public void PopulateList()
    {
        FoundFolders = [
            new("C:\\Wndws\\Help\\APKognito.2355.temp", 27349872928),
            new("C:\\Windws\\System32\\APKognto.6745ftemp", 8388392),
            new("C:\\Windws\\SsWow432\\APKogito.35422.temp", 2992),
            new("C:\\Winows\\ystem32\\PKognito.3847958temp", 234095728),
            new("C:\\Widows\\elp\\APKognito.2355.", 27349872928),
            new("C:\\Windows\\Sem32\\APKognio.645f.temp", 8388392),
            new("C:\\Wnds\\SysWo643\\APKognio.322.temp", 2992),
            new("C:\\Windows\\Sys\\APKognito.255.temp", 27349872928),
            new("C:\\Widows\\Systeow6432\\Aogni.35422.temp", 2992),
            new("C:\\Winows\\Sytem32\\APKognit355.temp", 27349872928),
            new("C:\\Winws\\Sstem32\\APKgnto.6745f.temp", 8388392),
            new("C:\\Windows\\SysWow632\\APKonto.322.temp", 2992),
            new("C:\\Wndows\\Help\\APKognit.235.temp", 27349872928),
            new("C:\\Winws\\System32\\APKognit.6745f.temp", 8388392),
            new("C:\\Windows\\SyWow32\\APKognto5422.temp", 2992),
            new("C:\\Widows\\Syse3\\ognio.3847958.temp", 234095728),
        ];
    }

#endif

    #endregion DEBUG_ONLY
}
