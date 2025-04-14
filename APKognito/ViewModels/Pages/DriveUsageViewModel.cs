using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Helpers;
using APKognito.Models;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Threading;

namespace APKognito.ViewModels.Pages;

public partial class DriveUsageViewModel : ViewModel, IViewable
{
    private const string CLAIM_FILE_NAME = ".apkognito";
    private const string PACKAGE_LIST_JOIN_STRING = "\n  ⚬  ";

    private readonly KognitoConfig kognitoConfig;

    private readonly List<FootprintInfo> cachedFootprints = [];

    private uint filter = 0;

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
    public partial string StartButtonText { get; set; } = "Refresh";

    [ObservableProperty]
    public partial long TotalUsedSpace { get; set; } = 0;

    [ObservableProperty]
    public partial long TotalFilteredSpace { get; set; } = 0;

    [ObservableProperty]
    public partial long TotalSelectedSpace { get; set; } = 0;

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
    public partial string CurrentlyDeleting { get; set; }

    [ObservableProperty]
    public partial string CurrentlyDeletingLow { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<FootprintInfo> FoundFolders { get; set; } = [];

    #endregion Properties

    public DriveUsageViewModel(ConfigurationFactory _configFactory)
    {
        kognitoConfig = _configFactory.GetConfig<KognitoConfig>();

        FilterInRenamedApks = false;
        FilterInFiles = FilterInDirectories = true;

#if DEBUG
        PopulateList();
#endif
    }

    #region Commands

    private CancellationTokenSource? _collectDataCancelationSource;

    [RelayCommand]
    public async Task StartSearchAsync()
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

        if (itemsToDelete.Count < 1)
        {
            goto Exit;
        }

        MessageBox confirmation = new()
        {
            Title = $"Delete {GetFormattedItems(itemsToDelete)}?",
            Content = $"This will remove the following items:{PACKAGE_LIST_JOIN_STRING}{GetFormattedSelectedPackages(itemsToDelete, PACKAGE_LIST_JOIN_STRING)}\n\nContinue?",
            PrimaryButtonText = "Delete",
            PrimaryButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger,
            CloseButtonText = "Cancel",
            MaxHeight = 600,
        };

        MessageBoxResult result = await confirmation.ShowDialogAsync();

        if (result is MessageBoxResult.Primary)
        {
            await DeleteFileCollectionAsync(itemsToDelete);
            FoundFolders.Clear();
        }

    Exit:
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

        if (cachedFootprints.Count < 1)
        {
            goto Exit;
        }

        MessageBox confirmation = new()
        {
            Title = $"Delete {GetFormattedItems(FoundFolders)}?",
            Content = $"This will remove the following items:{PACKAGE_LIST_JOIN_STRING}{GetFormattedSelectedPackages(FoundFolders, PACKAGE_LIST_JOIN_STRING)}\n\nContinue?",
            PrimaryButtonText = "Delete",
            PrimaryButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger,
            CloseButtonText = "Cancel",
            MaxHeight = 600
        };

        MessageBoxResult result = await confirmation.ShowDialogAsync();

        if (result != MessageBoxResult.Primary)
        {
            goto Exit;
        }

        await DeleteFileCollectionAsync(FoundFolders);
        FoundFolders.Clear();

    Exit:
        CanDelete = true;
        IsRunning = false;
        CurrentlyDeleting = CurrentlyDeletingLow = string.Empty;

        await StartSearchAsync();
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
                CanDelete = FileListVisibility = false;
                return;
            }

            // The filter is just a generic mask. Each item checks if the flag is set within the filter.
            foreach (FootprintInfo? item in cachedFootprints.Where(fp => filter == 0
                || ((FootprintTypes)filter).HasFlag(fp.ItemType)))
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
        folders.AddRange(Directory.GetDirectories(Path.GetTempPath(), "APKognito-*"));

        string apkOutputPath = kognitoConfig.ApkOutputDirectory ?? string.Empty;
        if (Directory.Exists(apkOutputPath))
        {
            apkOutputPath = Path.GetFullPath(apkOutputPath);
            folders.AddRange(Directory.GetDirectories(apkOutputPath).Where(IsDirectoryClaimed));
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

    public static void ClaimDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new ArgumentException($"Unable to claim directory '{directory}' as it doesn't exist.");
        }

        if (IsDirectoryClaimed(directory))
        {
            return;
        }

        string hiddenFile = Path.Combine(directory, CLAIM_FILE_NAME);
        File.Create(hiddenFile).Close();
        File.SetAttributes(hiddenFile, File.GetAttributes(hiddenFile) | FileAttributes.Hidden);
    }

    public static bool IsDirectoryClaimed(string directory)
    {
        return !Directory.Exists(directory)
            ? throw new ArgumentException($"Unable to check if directory is claimed '{directory}' as it doesn't exist.")
            : File.Exists(Path.Combine(directory, CLAIM_FILE_NAME));
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

    private static string GetFormattedSelectedPackages(IEnumerable<FootprintInfo> items, string joinStr)
    {
        return string.Join(joinStr, items.Select(item => item.FolderName));
    }

    private async Task DeleteFileCollectionAsync(IEnumerable<FootprintInfo> files)
    {
        // Horrible to the thread pool, but I'm not sure what else to do right now...
        // We can only hope the user run a deletion while renaming a package.
        await Task.Run(() =>
        {
            foreach (var entry in files)
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
                    Directory.Delete(entry.FolderPath);
                }
            }
        });

        void DeleteDirectory(string directory)
        {
            foreach (string file in Directory.EnumerateFileSystemEntries(directory).OrderByDescending(str => str.Length))
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
                        Directory.Delete(file);
                    }
                }
                catch
                {
                    // Skip
                    CurrentlyDeletingLow = "Failed to delete some files!";
                }
            }
        }
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