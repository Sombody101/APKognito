using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Models;
using APKognito.Utilities;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using Wpf.Ui;
using TreeViewItem = Wpf.Ui.Controls.TreeViewItem;

namespace APKognito.ViewModels.Pages;

public partial class FileExplorerViewModel : LoggableObservableObject, IViewable
{
    private readonly AdbConfig adbConfig = ConfigurationFactory.GetConfig<AdbConfig>();

    #region Properties

    [ObservableProperty]
    private ObservableCollection<AdbFolderInfo> _adbFolders = [];

    [ObservableProperty]
    private string _itemPath = "/";

    #endregion Properties

    public FileExplorerViewModel()
    {
#if DEBUG
        _adbFolders.Add(AdbFolderInfo.DebugFiller);
#endif
    }

    public FileExplorerViewModel(ISnackbarService _snackbarService)
    {
        SetSnackbarProvider(_snackbarService);
    }

    #region Commands

    [RelayCommand]
    private async Task OnTryConnection()
    {
        await GetFolders(null);
        
    }

    [RelayCommand]
    private async Task OnTryRefreshDirectory(AdbFolderInfo info)
    {
        TreeViewItem? itemToRefresh = null;
        if (info.ParentTreeViewItem is not null)
        { 
            var thing = info.ParentTreeViewItem.Items;
        }
        else
        {

        }

        await GetFolders(itemToRefresh);
    }

    #endregion Commands

    public async Task GetFolders(TreeViewItem? expandingItem)
    {
        if (string.IsNullOrWhiteSpace(adbConfig.CurrentDeviceId))
        {
            SnackError(
                "No device selected",
                "Cannot get directory or file information without a selected device"
            );

            return;
        }

        try
        {
            string basePath = string.Empty;

            if (expandingItem?.DataContext is AdbFolderInfo folderInfo)
            {
                basePath = folderInfo.FullPath;
            }

            bool isRoot = expandingItem is null;

            if (!isRoot
                && expandingItem!.ItemsSource is IEnumerable<AdbFolderInfo> presentFolderInfo
                && presentFolderInfo.First() != AdbFolderInfo.EmptyDirectory
                && presentFolderInfo.First() != AdbFolderInfo.EmptyLoading)
            {
                return;
            }

            string[] response = (await AdbManager.QuickDeviceCommand(
                $"shell stat -c {AdbFolderInfo.FormatString} {basePath}/* 2>/dev/null")).StdOut
                .Split("\r\n");

            if (response.Length is 1)
            {
                // If the response length doesn't include directories, then it can't be the root of the 
                // device. If that's the case, then your device is bricked.
                expandingItem!.ItemsSource = new List<AdbFolderInfo>() { AdbFolderInfo.EmptyDirectory };
                return;
            }

            // Get items here before giving the UI thread control
            var filtered = response
                .Where(str => !string.IsNullOrWhiteSpace(str))
                .Select(str => new AdbFolderInfo(str, expandingItem));
            AdbFolderInfo[] newItems = [.. filtered];

            await Dispatcher.CurrentDispatcher.InvokeAsync(() =>
            {
                if (isRoot)
                {
                    AdbFolders.Clear();

                    foreach (AdbFolderInfo? item in newItems)
                    {
                        AdbFolders.Add(item);
                    }
                }
                else
                {
                    expandingItem!.ItemsSource = newItems;
                }
            });
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);

            string forItem = expandingItem is null
                ? " for root directory"
                : $" for {(expandingItem.ItemsSource as IEnumerable<AdbFolderInfo>)?.First()?.FileName ?? string.Empty}";

            SnackError($"Failed to get directory descendants{forItem}", ex.Message);
        }
    }

    public void SelectFolder(TreeViewItem item)
    {
        ItemPath = ((AdbFolderInfo)item.DataContext).FullPath;
    }
}
