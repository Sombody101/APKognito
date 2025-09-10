using System.Collections.ObjectModel;
using System.ComponentModel;
using APKognito.ApkLib.Configuration;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Utilities;
using APKognito.Utilities.JavaTools;
using APKognito.Utilities.MVVM;
using Wpf.Ui;

namespace APKognito.ViewModels.Pages;

public sealed partial class RenameConfigurationViewModel : LoggableObservableObject, IDataErrorInfo
{
    private readonly UserRenameConfiguration _renameConfig;
    private readonly AdvancedApkRenameSettings _advancedSettings;
    private readonly CacheStorage _kognitoCache;
    private readonly ConfigurationFactory _configFactory;
    private readonly AdbConfig _adbConfig;

    public SharedViewModel SharedViewModel { get; }

    #region Properties

    [ObservableProperty]
    public partial ObservableCollection<JavaVersionInformation> FoundJavaVersions { get; set; } = [];

    [ObservableProperty]
    public partial JavaVersionInformation SelectedJavaVersion { get; set; }

    /// <summary>
    /// Creates a copy of the source files rather than moving them.
    /// Can help with data protection when a renaming session fails as APKognito cannot reverse the changes.
    /// </summary>
    public bool CopyWhenRenaming
    {
        get => _renameConfig.CopyFilesWhenRenaming;
        set => _renameConfig.CopyFilesWhenRenaming = value;
    }

    public bool PushAfterRename
    {
        get => _renameConfig.PushAfterRename;
        set
        {
            _renameConfig.PushAfterRename = value;
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
        get => _renameConfig.ApkOutputDirectory;
        set
        {
            value = VariablePathResolver.Resolve(value);
            _renameConfig.ApkOutputDirectory = value;
            OnPropertyChanged(nameof(OutputDirectory));
        }
    }

    /// <summary>
    /// The company name that will be used instead of the original APK company name
    /// </summary>
    public string ApkReplacementName
    {
        get => _renameConfig.ApkNameReplacement;
        set
        {
            if (value == _renameConfig.ApkNameReplacement)
            {
                return;
            }

            _renameConfig.ApkNameReplacement = value;
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

    [ObservableProperty]
    public partial string RenameObbsInternalExtras { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<ExtraPackageFileViewModel> ExtraPackageItems { get; set; } = [];

    public string JavaFlags
    {
        get => _advancedSettings.JavaFlags;
        set
        {
            _advancedSettings.JavaFlags = value;
            OnPropertyChanged(nameof(JavaFlags));
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

    public int SmaliCutoffLimit
    {
        get => _advancedSettings.SmaliCutoffLimit / 1024;
        set
        {
            value *= 1024;
            if (value == _advancedSettings.SmaliCutoffLimit)
            {
                return;
            }

            _advancedSettings.SmaliCutoffLimit = value;
            OnPropertyChanged(nameof(SmaliCutoffLimit));
        }
    }

    public int SmaliBufferSize
    {
        get => _advancedSettings.SmaliBufferSize / 1024;
        set
        {
            value *= 1024;
            if (value == _advancedSettings.SmaliBufferSize)
            {
                return;
            }

            _advancedSettings.SmaliBufferSize = value;
            OnPropertyChanged(nameof(SmaliBufferSize));
        }
    }

    public bool ScanFileBeforeRename
    {
        get => _advancedSettings.ScanFileBeforeRename;
        set
        {
            if (value == _advancedSettings.ScanFileBeforeRename)
            {
                return;
            }

            _advancedSettings.ScanFileBeforeRename = value;
            OnPropertyChanged(nameof(ScanFileBeforeRename));
        }
    }

    public string Error => null!;

    public bool AdbConfigured => !string.IsNullOrEmpty(_adbConfig.PlatformToolsPath);

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
        _renameConfig = null!;
        _advancedSettings = null!;
        _kognitoCache = null!;
        _configFactory = null!;
        _adbConfig = null!;
        SharedViewModel = null!;
        SelectedJavaVersion = null!;
    }

    public RenameConfigurationViewModel(
        ConfigurationFactory configFactory,
        SharedViewModel sharedViewModel,
        JavaVersionCollector javaCollector,
        ISnackbarService snackService
    ) : base(configFactory)
    {
        _renameConfig = configFactory.GetConfig<UserRenameConfiguration>();
        _kognitoCache = configFactory.GetConfig<CacheStorage>();
        _advancedSettings = configFactory.GetConfig<AdvancedApkRenameSettings>();
        _adbConfig = configFactory.GetConfig<AdbConfig>();
        _configFactory = configFactory;

        SharedViewModel = sharedViewModel;

        SetSnackbarProvider(snackService);

        foreach (JavaVersionInformation javaVersion in javaCollector.JavaVersions)
        {
            FoundJavaVersions.Add(javaVersion);
        }

        SelectedJavaVersion = FoundJavaVersions.FirstOrDefault(i => i.RawVersion == _renameConfig.SelectedRawJavaVersion) ?? FoundJavaVersions.FirstOrDefault()!;
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
    private void OnResetField(string name)
    {
        switch (name)
        {
            case "regex":
                PackageReplaceRegexString = AdvancedApkRenameSettings.DEFAULT_RENAME_REGEX;
                return;

            case "java_flags":
                JavaFlags = AdvancedApkRenameSettings.DEFAULT_JAVA_ADDED_FLAGS;
                return;

            default:
                SnackError("Unknown field!", $"Unknown field '{name}'. Cannot reset.");
                return;
        }
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
        _configFactory.SaveConfigs(_kognitoCache, _renameConfig, _advancedSettings);
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

    partial void OnSelectedJavaVersionChanged(JavaVersionInformation value)
    {
        if (value is null)
        {
            return;
        }

        _renameConfig.SelectedRawJavaVersion = value.RawVersion;
    }

    public sealed partial class ExtraPackageFileViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial string FilePath { get; set; } = string.Empty;

        [ObservableProperty]
        public partial FileType FileType { get; set; } = FileType.RegularText;

        public static implicit operator ExtraPackageFile(ExtraPackageFileViewModel viewModel) => new()
        {
            FilePath = viewModel.FilePath.TrimStart('/', '\\'),
            FileType = viewModel.FileType
        };

        public static implicit operator ExtraPackageFileViewModel(ExtraPackageFile model) => new()
        {
            FilePath = model.FilePath,
            FileType = model.FileType
        };
    }
}
