﻿using APKognito.Configurations;
using APKognito.Models;
using APKognito.Models.Settings;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Wpf.Ui.Controls;

namespace APKognito.ViewModels.Pages;

public partial class DriveUsageViewModel : ObservableObject, IViewable
{
    private readonly KognitoConfig config;

    #region Properties

    [ObservableProperty]
    private Visibility _isRunning =
#if DEBUG
        Visibility.Visible;

#else
        Visibility.Hidden;
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
    private bool _canDelete = false;

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
    }

    #region Commands

    private CancellationTokenSource? _collectDataCancelationSource;

    [RelayCommand]
    public async Task StartSearch()
    {
        if (IsRunning == Visibility.Visible)
        {
            StartButtonText = "Refresh";
            IsRunning = Visibility.Hidden;

            _ = (_collectDataCancelationSource?.CancelAsync());
        }
        else
        {
            StartButtonText = "Cancel";
            IsRunning = Visibility.Visible;

            FoundFolders.Clear();
            TotalUsedSpace = 0;
            _collectDataCancelationSource ??= new();

            try
            {
                await CollectDiskUsage(_collectDataCancelationSource.Token);
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
                IsRunning = Visibility.Hidden;
            }
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedItems(ListView folderList)
    {
        CanDelete = false;
        IsRunning = Visibility.Visible;

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

        foreach (FootprintInfo? item in itemsToDelete)
        {
            _ = FoundFolders.Remove(item);
        }

        CanDelete = true;
        IsRunning = Visibility.Hidden;

        await StartSearch();
    }

    [RelayCommand]
    private async Task DeleteAllItems()
    {
        CanDelete = false;
        IsRunning = Visibility.Visible;

        Wpf.Ui.Controls.MessageBox confirmation = new()
        {
            Title = $"Delete {GetFormattedItems()}?",
            Content = "All renamed APKs, OBBs, remaining temporary directories, tools, etc, will be deleted.\n\nContinue?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
        };

        MessageBoxResult result = await confirmation.ShowDialogAsync();

        if (result != MessageBoxResult.Primary)
        {
            goto Exit;
        }

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
        IsRunning = Visibility.Hidden;

        await StartSearch();
    }

    #endregion Commands

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

        // Use a loop to add items individually to the ObservableCollection
        foreach (FootprintInfo? folderStat in folderStats)
        {
            FoundFolders.Add(folderStat);
        }

        TotalUsedSpace = folderStats.Sum(f => f.FolderSizeBytes);

        if (FoundFolders.Count is 0)
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
    }

    private string GetFormattedItems()
    {
        int folderCount = 0, fileCount = 0, apkCount = 0;

        foreach (var item in FoundFolders)
        {
            switch (item.ItemType)
            {
                case FootprintType.Directory:
                    folderCount++;
                    break;
                case FootprintType.File:
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

        return sb.ToString();
    }

    private static async Task<long> DirSizeAsync(DirectoryInfo d, CancellationToken cancellation)
    {
        long size = 0;

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

        foreach (Task<long> task in tasks)
        {
            size += await task;
        }

        return size;
    }
}