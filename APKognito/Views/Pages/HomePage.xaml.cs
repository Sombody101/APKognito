using APKognito.Configurations;
using APKognito.Models.Settings;
using APKognito.ViewModels.Pages;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Wpf.Ui.Controls;

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
            HomeViewModel.WriteGenericLog("\n@ Welcome! Load an APK to get started! @\n");
        }
        else
        {
            HomeViewModel.WriteGenericLog($"@ Press 'Start' to rename your APK{(loadedFiles.Length is 1 ? string.Empty : 's')}! @\n");
            viewModel.ApkName = Path.GetFileName(viewModel.FilePath);
            viewModel.UpdateCanStart();
        }
    }

    private void UpdateLogs(object sender, TextChangedEventArgs e)
    {
        APKLogs.ScrollToEnd();
    }

    private void TextBox_KeyUp(object sender, KeyEventArgs e)
    {
        TextBox tBox = (TextBox)sender;
        DependencyProperty prop = TextBox.TextProperty;

        BindingExpression binding = BindingOperations.GetBindingExpression(tBox, prop);
        binding?.UpdateSource();
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        App.OpenHyperlink(sender, e);
    }

    private void Page_DragOver(object sender, DragEventArgs e)
    {
    }

    private void Page_Drop(object sender, DragEventArgs e)
    {
    }

    private void CheckBox_Checked(object sender, RoutedEventArgs e)
    {
        ViewModel.OnRenameCopyChecked();
    }
}