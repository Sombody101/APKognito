using APKognito.Models;
using System.Collections.ObjectModel;
using System.IO;
using Wpf.Ui.Controls;

using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace APKognito.ViewModels.Pages;

public partial class DriveUsageViewModel : ObservableObject, IViewable
{
    #region Properties

    [ObservableProperty]
    private Visibility _isRunning =
#if DEBUG
        Visibility.Visible;
#else
        Visibility.Hidden;
#endif

    [ObservableProperty]
    private string _startButtonText = "Get Drive Footprint";

    [ObservableProperty]
    private int _totalUsedSpace = 0;

    [ObservableProperty]
    private bool _canDelete = false;

    [ObservableProperty]
    private ObservableCollection<DriveFolderStat> _foundFolders = [
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

    #region Commands

    private CancellationTokenSource? _collectDataCancelationSource;

    [RelayCommand]
    private void StartSearch()
    {
        if (IsRunning == Visibility.Visible)
        {
            StartButtonText = "Get Drive Footprint";
            IsRunning = Visibility.Hidden;

            _collectDataCancelationSource?.Cancel();
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
                CollectDiskUsage(_collectDataCancelationSource.Token);
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation
            }
            finally
            {
                _collectDataCancelationSource.Dispose();
                _collectDataCancelationSource = null;
                StartButtonText = "Get Drive Footprint";
                IsRunning = Visibility.Hidden;
            }
        }
    }

    [RelayCommand]
    private void DeleteSelectedItems(ListView folderList)
    {
        CanDelete = false;
        IsRunning = Visibility.Visible;

        List<DriveFolderStat> itemsToDelete = folderList.SelectedItems.Cast<DriveFolderStat>().ToList();

        foreach (DriveFolderStat? item in itemsToDelete)
        {
            if (item.IsFile)
            {
                File.Delete(item.FolderPath);
            }
            else
            {
                Directory.Delete(item.FolderPath, true);
            }
        }

        foreach (DriveFolderStat? item in itemsToDelete)
        {
            _ = FoundFolders.Remove(item);
        }

        CanDelete = true;
        IsRunning = Visibility.Hidden;
    }

    [RelayCommand]
    private async Task DeleteAllItems()
    {
        CanDelete = false;
        IsRunning = Visibility.Visible;

        Wpf.Ui.Controls.MessageBox confirmation = new()
        {
            Title = $"Delete all {FoundFolders.Count} files and folders?",
            Content = "All files and folders created by APKognito, but not the app itself, will be deleted. \nContinue?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
        };

        MessageBoxResult result = await confirmation.ShowDialogAsync();

        if (result != MessageBoxResult.Primary)
        {
            goto Exit;
        }

        foreach (DriveFolderStat item in FoundFolders)
        {
            if (item.IsFile)
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
    }

    #endregion

    private void CollectDiskUsage(CancellationToken cancellation)
    {
        List<string> folders = [];
        folders.AddRange(Directory.GetDirectories(Path.GetTempPath(), "APKognito-*"));

        string apkOutputPath = HomeViewModel.Instance!.OutputPath;
        if (Directory.Exists(apkOutputPath))
        {
            apkOutputPath = Path.GetFullPath(apkOutputPath);
            folders.AddRange(Directory.GetDirectories(apkOutputPath));
            folders.AddRange(Directory.GetFiles(apkOutputPath));
        }

        foreach (string folderName in folders)
        {
            if (cancellation.IsCancellationRequested)
            {
                break;
            }

            FileAttributes attrs = File.GetAttributes(folderName);
            if (attrs.HasFlag(FileAttributes.Directory))
            {
                DirectoryInfo di = new(folderName);
                long size = DirSize(di);
                FoundFolders.Add(new DriveFolderStat(di, size));
                TotalUsedSpace += (int)(size / 1024 / 1024);
            }
            else
            {
                FileInfo fi = new(folderName);
                FoundFolders.Add(new DriveFolderStat(fi));
                TotalUsedSpace += (int)(fi.Length / 1024 / 1024);
            }
        }

        if (FoundFolders.Count is 0)
        {
            FoundFolders.Add(DriveFolderStat.Empty);
            CanDelete = false;
        }
        else
        {
            CanDelete = true;
        }
    }

    private static long DirSize(DirectoryInfo d)
    {
        long size = 0;
        FileInfo[] fis = d.GetFiles();
        foreach (FileInfo fi in fis)
        {
            size += fi.Length;
        }

        DirectoryInfo[] dis = d.GetDirectories();
        foreach (DirectoryInfo di in dis)
        {
            size += DirSize(di);
        }

        return size;
    }
}
