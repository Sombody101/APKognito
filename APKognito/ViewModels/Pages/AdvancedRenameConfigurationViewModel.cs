using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Utilities.MVVM;

namespace APKognito.ViewModels.Pages;

public partial class AdvancedRenameConfigurationViewModel : ViewModel, IViewable
{
    private AdvancedApkRenameSettings advancedSettings;

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

    #endregion Properties

    public AdvancedRenameConfigurationViewModel()
    {
        advancedSettings = ConfigurationFactory.Instance.GetConfig<AdvancedApkRenameSettings>();

        RenameObbsInternalExtras = string.Join('\n', advancedSettings.RenameObbsInternalExtras);
    }

    #region Commands

    [RelayCommand]
    private void OnUpdateRenameObbsInternalExtras()
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

    #endregion Commands
}
