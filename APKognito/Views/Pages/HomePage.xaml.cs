using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Pages;
using System.IO;
using Wpf.Ui.Abstractions.Controls;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace APKognito.Views.Pages;

public partial class HomePage : INavigableView<HomeViewModel>, IViewable
{
    [MemberNotNull]
    public static HomePage? Instance { get; private set; }

    public HomeViewModel ViewModel { get; }
    public KognitoConfig Config { get; init; }

    public HomePage(HomeViewModel viewModel)
    {
        Instance = this;

        ViewModel = viewModel;
        DataContext = ViewModel;

        InitializeComponent();
        viewModel.AntiMvvm_SetRichTextbox(APKLogs);

        Config = ConfigurationFactory.Instance.GetConfig<KognitoConfig>();

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

        Loaded += async (sender, e) => await ViewModel.InitializeAsync();
    }

    private void UpdateLogs(object sender, TextChangedEventArgs e)
    {
        APKLogs.ScrollToEnd();
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        App.OpenHyperlink(sender, e);
    }

    private async void CheckBox_CheckedAsync(object sender, RoutedEventArgs e)
    {
        await ViewModel.OnRenameCopyCheckedAsync();
    }

    private void TextBox_KeyUp(object sender, KeyEventArgs e)
    {
        App.ForwardKeystrokeToBinding(sender);
    }

    private void Card_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            DragDropPresenter.Visibility = Visibility.Visible;
            e.Handled = true;
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
        DragDropPresenter.Visibility = Visibility.Collapsed;
    }
}