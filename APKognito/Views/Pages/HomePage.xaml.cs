using APKognito.Configurations;
using APKognito.Controls;
using APKognito.Models.Settings;
using APKognito.ViewModels.Pages;
using System.IO;
using System.Windows.Data;
using Wpf.Ui.Controls;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;

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

        Config = ConfigurationFactory.GetConfig<KognitoConfig>();

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

        Loaded += async (sender, e) => await ViewModel.Initialize();
    }

    private void UpdateLogs(object sender, TextChangedEventArgs e)
    {
        APKLogs.ScrollToEnd();
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        App.OpenHyperlink(sender, e);
    }

    private async void CheckBox_Checked(object sender, RoutedEventArgs e)
    {
        await ViewModel.OnRenameCopyChecked();
    }

    private void TextBox_KeyUp(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tBox)
        {
            return;
        }

        DependencyProperty prop = TextBox.TextProperty;

        BindingExpression binding = BindingOperations.GetBindingExpression(tBox, prop);
        binding?.UpdateSource();
    }

    private void Card_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            DragDropPresenter.Visibility = Visibility.Visible;
            e.Handled = true;
        }
    }

    private async void DragDropPresenter_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            await ViewModel.AddManualFiles(files);
            DragDropPresenter.Visibility = Visibility.Collapsed;
        }
    }

    private void DragDropPresenter_PreviewDragLeave(object sender, DragEventArgs e)
    {
        DragDropPresenter.Visibility = Visibility.Collapsed;
    }
}