using APKognito.Models.Settings;
using APKognito.ViewModels.Pages;
using Microsoft.Win32;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Windows.Controls;
using Wpf.Ui.Controls;

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

        Config = KognitoSettings.GetSettings();

        viewModel.Logs = File.Exists(viewModel.FilePath)
            ? $"@ Press 'Start' to rename {viewModel.ApkName}! @"
            : "@ Welcome! Load an APK to get started! @";

        viewModel.Logs = "<--> =>";

        viewModel.FilePath = "this:is:a:test";
    }

    private void UpdateLogs(object sender, TextChangedEventArgs e)
    {
        APKLogs.SelectionStart = APKLogs.Text.Length;
        APKLogs.ScrollToEnd();
    }

    /// <summary>
    /// Uses File Explorer to open an APK
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public void LoadApk(object sender, RoutedEventArgs e)
    {
        OpenFileDialog openFileDialog = new()
        {
            Filter = "APK files (*.apk)|*.apk",
            Multiselect = true,
            DefaultDirectory = Config.LastDialogDirectory
        };

        bool? result = openFileDialog.ShowDialog();

        if (result is null)
        {
            // If this is giving 
            ViewModel.Log("Failed to get file. Please try again.");
            return;
        }

        if ((bool)result)
        {
            ViewModel.ClearLogs();
            string[] selectedFilePaths = openFileDialog.FileNames;

            if (selectedFilePaths.Length is 1)
            {
                string selectedFilePath = selectedFilePaths[0];
                ViewModel.FilePath = selectedFilePath;
                string apkName = ViewModel.ApkName = Path.GetFileName(selectedFilePath);
                ViewModel.Log($"Selected {apkName} from: {selectedFilePath}");
            }
            else
            {
                ViewModel.FilePath = string.Join(':', selectedFilePaths);

                StringBuilder sb = new($"Selected {selectedFilePaths.Length} APKs");

                foreach (string str in selectedFilePaths)
                {
                    _ = sb.Append("\tFrom: ").Append(str);
                }

                ViewModel.Log(sb.ToString());
            }
        }
        else
        {
            ViewModel.Log("Did you forget to select a file from the File Explorer window?");
        }
    }

    public void StartApkRename(object sender, RoutedEventArgs e)
    {
        ViewModel.LogWarning("Hello");
        ViewModel.LogError("Hello");
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        _ = Process.Start(new ProcessStartInfo()
        {
            FileName = e.Uri.ToString(),

            // Starts it in the default browser. Otherwise, it will look for a file using the URL as a file path
            UseShellExecute = true
        });

        // If not handled, the page is rendered in the WPF page... (or at least attempted, because the CSS doesn't load lol)
        e.Handled = true;
    }
}
