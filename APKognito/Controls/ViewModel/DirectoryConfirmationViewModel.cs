using APKognito.Configurations;
using APKognito.Models.Settings;
using APKognito.Utilities;
using Microsoft.Win32;
using System.IO;

namespace APKognito.Controls.ViewModel;

public partial class DirectoryConfirmationViewModel : ObservableObject
{
    private readonly KognitoConfig kognitoConfig = ConfigurationFactory.Instance.GetConfig<KognitoConfig>();

    #region Properties

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _content;

    public string OutputDirectory
    {
        get => kognitoConfig.ApkPullDirectory;
        set
        {
            value = VariablePathResolver.Resolve(value);
            kognitoConfig.ApkPullDirectory = value;
            OnPropertyChanged(nameof(OutputDirectory));
        }
    }

    #endregion Properties

    public DirectoryConfirmationViewModel()
    {
        // For designer
    }

    #region Commands

    [RelayCommand]
    private void OnChooseDirectory()
    {
        SelectDirectory();
    }

    #endregion Commands

    private void SelectDirectory()
    {
        string? oldOutput = OutputDirectory;

        if (!Directory.Exists(oldOutput))
        {
            oldOutput = null;
        }

        OpenFolderDialog openFolderDialog = new()
        {
            Multiselect = false,
            DefaultDirectory = oldOutput ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };

        if (openFolderDialog.ShowDialog() is false
            && openFolderDialog.FolderNames.Length is 0)
        {
            return;
        }

        OutputDirectory = openFolderDialog.FolderName;
    }
}
