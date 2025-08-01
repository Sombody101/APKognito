﻿using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Utilities;
using Microsoft.Win32;
using System.IO;

namespace APKognito.Controls.ViewModels;

public partial class DirectoryConfirmationViewModel : ObservableObject
{
    private readonly UserRenameConfiguration kognitoConfig = App.GetService<ConfigurationFactory>()!.GetConfig<UserRenameConfiguration>();

    #region Properties

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Content { get; set; } = string.Empty;

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
