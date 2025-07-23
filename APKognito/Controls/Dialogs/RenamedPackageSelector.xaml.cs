using APKognito.ApkMod;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Helpers;
using APKognito.Utilities;
using System.Collections.ObjectModel;
using System.IO;
using Wpf.Ui.Controls;

namespace APKognito.Controls.Dialogs;

/// <summary>
/// Interaction logic for RenamedPackageSelector.xaml
/// </summary>
public partial class RenamedPackageSelector : ContentDialog
{
    private readonly ConfigurationFactory _configFactory;

    public RenamedPackageSelectorViewModel ViewModel { get; private set; }

    public PresentableRenamedPackageMetadata SelectedItem => (PresentableRenamedPackageMetadata)MetadataPresenter.SelectedItem;

    public RenamedPackageSelector()
    {
        InitializeComponent();
    }

    public RenamedPackageSelector(ConfigurationFactory configFactory, ContentPresenter? contentPresenter)
        : base(contentPresenter)
    {
        DataContext = this;
        ViewModel = new();
        _configFactory = configFactory;

        InitializeComponent();
    }

    protected override void OnLoaded()
    {
        Task.Run(RefreshMetadataListAsync);
    }

    public async Task RefreshMetadataListAsync()
    {
        string outputDirectory = _configFactory.GetConfig<Configurations.ConfigModels.UserRenameConfiguration>().ApkOutputDirectory;

        if (string.IsNullOrEmpty(outputDirectory) || !Directory.Exists(outputDirectory))
        {
            return;
        }

        List<PresentableRenamedPackageMetadata> found = [];

        foreach (string directory in Directory.EnumerateDirectories(outputDirectory))
        {
            try
            {
                if (!DirectoryManager.TryGetClaimFile(directory, out string? claimFile))
                {
                    continue;
                }

                RenamedPackageMetadata? loadedMetadata = MetadataManager.LoadMetadata(claimFile);

                if (loadedMetadata is null)
                {
                    continue;
                }

                ulong assetsSize = loadedMetadata.RelativeAssetsPath is not null
                    ? await DirectoryManager.DirSizeAsync(Path.GetFullPath(Path.Combine(directory, loadedMetadata.RelativeAssetsPath)))
                    : 0;

                var foundMetadata = new PresentableRenamedPackageMetadata()
                {
                    PackagePath = Path.Combine(directory, $"{loadedMetadata.PackageName}.apk"),
                    Metadata = loadedMetadata,
                    AssetsSize = assetsSize
                };

                found.Add(foundMetadata);
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex);
            }
        }

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (found.Count is 0)
            {
                ViewModel.HideListView = true;
                return;
            }

            foreach (var item in found)
            {
                ViewModel.FoundPackages.Add(item);
            }
        });
    }

    public partial class RenamedPackageSelectorViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial ObservableCollection<PresentableRenamedPackageMetadata> FoundPackages { get; set; } = [];

        [ObservableProperty]
        public partial bool HideListView { get; set; } = false;
    }
}

public record PresentableRenamedPackageMetadata
{
    public required string PackagePath { get; set; }

    public ulong AssetsSize { get; set; }

    public required RenamedPackageMetadata Metadata { get; set; }

    public string FormattedAssetsSize => GBConverter.FormatSizeFromBytes(AssetsSize);
}
