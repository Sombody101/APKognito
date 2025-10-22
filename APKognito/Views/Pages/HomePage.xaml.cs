using System.Diagnostics;
using System.IO;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace APKognito.Views.Pages;

public partial class HomePage : INavigableView<HomeViewModel>, IViewable
{
    public HomeViewModel ViewModel { get; }

    public HomePage()
    {
        // For Designer
        ViewModel = new(default!, new(), default!, new());
    }

    public HomePage(HomeViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();

        string[]? loadedFiles = viewModel.GetFilePaths();
        if (loadedFiles is null || loadedFiles.Length is 0)
        {
            viewModel.WriteGenericLog("\n@ Welcome! Load an APK to get started! @\n");
        }
        else
        {
            viewModel.WriteGenericLog($"@ Press 'Start' to rename your APK{(loadedFiles.Length is 1 ? string.Empty : 's')}! @\n");
            viewModel.ApkName = Path.GetFileName(viewModel.FilePath);
            viewModel.UpdateCanStart();
        }
    }

    private void UpdateLogs(object sender, TextChangedEventArgs e)
    {
        APKLogs.ScrollToEnd();
    }

    private static void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        App.OpenHyperlink(sender, e);
    }

    private async void CheckBox_CheckedAsync(object sender, RoutedEventArgs e)
    {
        await ViewModel.OnRenameCopyCheckedAsync();
    }

    [CalledByGenerated]
    private void TextBox_KeyUp(object sender, KeyEventArgs e)
    {
        App.ForwardKeystrokeToBinding(sender);
    }

    private bool _dragOverDebounce = false;
    private void Card_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (!_dragOverDebounce || e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            Debug.WriteLine("Drag enter");
            DragDropPresenter.Visibility = Visibility.Visible;
            _dragOverDebounce = e.Handled = true;
        }
    }

    private async void DragDropPresenter_DropAsync(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            await ViewModel.AddManualFilesAsync(files);
            DragDropPresenter.Visibility = Visibility.Collapsed;
        }
    }

    private void DragDropPresenter_PreviewDragLeave(object sender, DragEventArgs e)
    {
        Debug.WriteLine("Drag leave");
        DragDropPresenter.Visibility = Visibility.Collapsed;
        _dragOverDebounce = false;
    }
}
