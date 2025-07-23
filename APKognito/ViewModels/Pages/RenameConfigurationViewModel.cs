using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using APKognito.ApkLib.Configuration;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using Wpf.Ui;

namespace APKognito.ViewModels.Pages;

public sealed partial class RenameConfigurationViewModel : LoggableObservableObject, IDataErrorInfo
{
    private readonly UserRenameConfiguration _kognitoConfig;
    private readonly AdvancedApkRenameSettings _advancedSettings;
    private readonly CacheStorage _kognitoCache;
    private readonly ConfigurationFactory _configFactory;
    private readonly AdbConfig _adbConfig;

    public SharedViewModel SharedViewModel { get; }

    #region Properties

    public string JavaExecutablePath
    {
        get => _kognitoConfig.ToolingPaths.JavaExecutablePath;
        set
        {
            _kognitoConfig.ToolingPaths.JavaExecutablePath = value;
            OnPropertyChanged(nameof(JavaExecutablePath));
        }
    }

    /// <summary>
    /// Creates a copy of the source files rather than moving them.
    /// Can help with data protection when a renaming session fails as APKognito cannot reverse the changes.
    /// </summary>
    public bool CopyWhenRenaming
    {
        get => _kognitoConfig.CopyFilesWhenRenaming;
        set => _kognitoConfig.CopyFilesWhenRenaming = value;
    }

    public bool PushAfterRename
    {
        get => _kognitoConfig.PushAfterRename;
        set
        {
            _kognitoConfig.PushAfterRename = value;
            OnPropertyChanged(nameof(PushAfterRename));
        }
    }

    /// <summary>
    /// A string of all APK paths separated by <see cref="PATH_SEPARATOR"/>
    /// </summary>
    public string FilePath
    {
        get => _kognitoCache?.ApkSourcePath ?? HomeViewModel.DEFAULT_PROP_MESSAGE;
        set
        {
            _kognitoCache.ApkSourcePath = value;
            OnPropertyChanged(nameof(FilePath));
        }
    }

    /// <summary>
    /// The directory path for all renamed APKs
    /// </summary>
    public string OutputDirectory
    {
        get => _kognitoConfig.ApkOutputDirectory;
        set
        {
            value = VariablePathResolver.Resolve(value);
            _kognitoConfig.ApkOutputDirectory = value;
            OnPropertyChanged(nameof(OutputDirectory));
        }
    }

    /// <summary>
    /// The company name that will be used instead of the original APK company name
    /// </summary>
    public string ApkReplacementName
    {
        get => _kognitoConfig.ApkNameReplacement;
        set
        {
            if (value == _kognitoConfig.ApkNameReplacement)
            {
                return;
            }

            _kognitoConfig.ApkNameReplacement = value;
            OnPropertyChanged(nameof(ApkReplacementName));
        }
    }

    // Advanced Config

    public string PackageReplaceRegexString
    {
        get => _advancedSettings.PackageReplaceRegexString;
        set
        {
            _advancedSettings.PackageReplaceRegexString = value;
            OnPropertyChanged(nameof(PackageReplaceRegexString));
        }
    }

    public bool RenameLibs
    {
        get => _advancedSettings.RenameLibs;
        set
        {
            _advancedSettings.RenameLibs = value;
            OnPropertyChanged(nameof(RenameLibs));
        }
    }

    public bool RenameLibsInternal
    {
        get => _advancedSettings.RenameLibsInternal;
        set
        {
            _advancedSettings.RenameLibsInternal = value;
            OnPropertyChanged(nameof(RenameLibsInternal));
        }
    }

    public bool RenameObbsInternal
    {
        get => _advancedSettings.RenameObbsInternal;
        set
        {
            _advancedSettings.RenameObbsInternal = value;
            OnPropertyChanged(nameof(RenameObbsInternal));
        }
    }

    public bool AutoPackageEnabled
    {
        get => _advancedSettings.AutoPackageEnabled;
        set
        {
            _advancedSettings.AutoPackageEnabled = value;
            OnPropertyChanged(nameof(AutoPackageEnabled));
        }
    }

    public string AutoPackageConfig
    {
        get => _advancedSettings.AutoPackageConfig ?? "; Visit the APKognito Wiki if you want to learn how to use this!";
        set
        {
            _advancedSettings.AutoPackageConfig = value;
            OnPropertyChanged(nameof(AutoPackageConfig));
        }
    }

    public bool AdbConfigured => !string.IsNullOrEmpty(_adbConfig.PlatformToolsPath);

    [ObservableProperty]
    public partial string RenameObbsInternalExtras { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<ExtraPackageFileViewModel> ExtraPackageItems { get; set; } = [];

    public string Error => null!;

    public string this[string columnName]
    {
        get
        {
            string? error = null;

            if (columnName == nameof(ApkReplacementName))
            {
                // Nothing for now
            }

            return error!;
        }
    }

    #endregion Properties

    public RenameConfigurationViewModel()
    {
        // For designer
        _kognitoConfig = null!;
        _advancedSettings = null!;
        _kognitoCache = null!;
        _configFactory = null!;
        _adbConfig = null!;
        SharedViewModel = null!;
    }

    public RenameConfigurationViewModel(
        ISnackbarService snackService,
        ConfigurationFactory configFactory,
        SharedViewModel sharedViewModel
    )
    {
        _kognitoConfig = configFactory.GetConfig<UserRenameConfiguration>();
        _kognitoCache = configFactory.GetConfig<CacheStorage>();
        _advancedSettings = configFactory.GetConfig<AdvancedApkRenameSettings>();
        _adbConfig = configFactory.GetConfig<AdbConfig>();
        _configFactory = configFactory;

        SharedViewModel = sharedViewModel;

        SetSnackbarProvider(snackService);
    }

    #region Commands

    // Advanced Configs

    [RelayCommand]
    private void OnSaveConfiguration()
    {
        _configFactory.SaveConfig(_advancedSettings);
    }

    [RelayCommand]
    private void OnApplyRenameObbsInternalExtras()
    {
        _advancedSettings.RenameObbsInternalExtras = [.. RenameObbsInternalExtras
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
        IEnumerable<ExtraPackageFile> formattedPaths = ExtraPackageItems.Select(item => (ExtraPackageFile)item);
        _advancedSettings.ExtraInternalPackagePaths = [.. formattedPaths];
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

    [RelayCommand]
    private void OnSaveSettings()
    {
        _configFactory.SaveConfigs(_kognitoCache, _kognitoConfig, _advancedSettings);
        Log("Settings saved!");
    }

    #endregion Commands

    public static async ValueTask OnRenameCopyCheckedAsync()
    {
        HomeViewModel? mainPageVm = App.GetService<HomeViewModel>();

        if (mainPageVm is null)
        {
            return;
        }

        await mainPageVm.RefreshValuesAsync();
    }

    public override void OnNavigatedTo()
    {
        // Just refreshes it when the user navigates to the config page again
        OnPropertyChanged(nameof(AdbConfigured));

        base.OnNavigatedTo();
    }

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
