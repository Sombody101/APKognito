using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Models;
using APKognito.Utilities.MVVM;
using System.Collections.ObjectModel;

namespace APKognito.ViewModels.Pages;

public partial class AdvancedRenameConfigurationViewModel : ViewModel, IViewable
{
    private readonly ConfigurationFactory configFactory;
    private readonly AdvancedApkRenameSettings advancedSettings;

    #region Properties

    public string PackageReplaceRegexString
    {
        get => advancedSettings.PackageReplaceRegexString;
        set
        {
            advancedSettings.PackageReplaceRegexString = value;
            OnPropertyChanged(nameof(PackageReplaceRegexString));
        }
    }

    public bool RenameLibs
    {
        get => advancedSettings.RenameLibs;
        set
        {
            advancedSettings.RenameLibs = value;
            OnPropertyChanged(nameof(RenameLibs));
        }
    }

    public bool RenameLibsInternal
    {
        get => advancedSettings.RenameLibsInternal;
        set
        {
            advancedSettings.RenameLibsInternal = value;
            OnPropertyChanged(nameof(RenameLibsInternal));
        }
    }

    public bool RenameObbsInternal
    {
        get => advancedSettings.RenameObbsInternal;
        set
        {
            advancedSettings.RenameObbsInternal = value;
            OnPropertyChanged(nameof(RenameObbsInternal));
        }
    }

    [ObservableProperty]
    public partial string RenameObbsInternalExtras { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<ExtraPackageFileViewModel> ExtraPackageItems { get; set; }

    #endregion Properties

    public AdvancedRenameConfigurationViewModel(ConfigurationFactory configFactory)
    {
        this.configFactory = configFactory;
        advancedSettings = configFactory.GetConfig<AdvancedApkRenameSettings>();

        RenameObbsInternalExtras = string.Join('\n', advancedSettings.RenameObbsInternalExtras);
        ExtraPackageItems = [.. advancedSettings.ExtraInternalPackagePaths.Select(item => (ExtraPackageFileViewModel)item)];
    }

    #region Commands

    [RelayCommand]
    private void OnSaveConfiguration()
    {
        configFactory.SaveConfig(advancedSettings);
    }

    [RelayCommand]
    private void OnApplyRenameObbsInternalExtras()
    {
        advancedSettings.RenameObbsInternalExtras = [.. RenameObbsInternalExtras
            .Split(["\r\n", ","], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())];
    }

    [RelayCommand]
    private void OnResetRegex()
    {
        PackageReplaceRegexString = AdvancedApkRenameSettings.DEFAULT_RENAME_REGEX;
    }

    [RelayCommand]
    private void OnAddExtraPathCard()
    {
        ExtraPackageFileViewModel newItem = new();
        ExtraPackageItems.Add(newItem);
    }

    [RelayCommand]
    private void OnApplyExtraPathChanges()
    {
        var formattedPaths = ExtraPackageItems.Select(item => (ExtraPackageFile)item);
        advancedSettings.ExtraInternalPackagePaths = [.. formattedPaths];
    }

    [RelayCommand]
    private void OnDeleteExtraPathCard(object _item)
    {
        if (_item is not ExtraPackageFileViewModel item)
        {
            return;
        }

        int itemIndex = ExtraPackageItems.IndexOf(item);

        if (itemIndex is not -1)
        {
            ExtraPackageItems.RemoveAt(itemIndex);
        }
    }

    #endregion Commands

    public sealed partial class ExtraPackageFileViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial string FilePath { get; set; } = string.Empty;

        [ObservableProperty]
        public partial FileType FileType { get; set; } = FileType.RegularText;

        public static implicit operator ExtraPackageFile(ExtraPackageFileViewModel viewModel)
        {
            return new ExtraPackageFile
            {
                FilePath = viewModel.FilePath.TrimStart('/', '\\'),
                FileType = viewModel.FileType
            };
        }

        public static implicit operator ExtraPackageFileViewModel(ExtraPackageFile model)
        {
            return new ExtraPackageFileViewModel
            {
                FilePath = model.FilePath,
                FileType = model.FileType
            };
        }
    }
}
