using APKognito.ApkLib.Configuration;
using APKognito.ApkLib.Editors;
using APKognito.ApkLib.Exceptions;
using APKognito.ApkLib.Interfaces;
using Microsoft.Extensions.Logging;

namespace APKognito.ApkLib;

public sealed class PackageEditorContext : IReportable<PackageEditorContext>
{
    private readonly ILogger _logger;
    private readonly PackageToolingPaths _toolingPaths;
    private readonly PackageNameData _nameData;
    private IProgress<ProgressInfo>? _reporter;

    internal PackageRenameConfiguration? RenameConfiguration { get; private set; }

    /// <summary>
    /// Creates a new <see cref="PackageEditorContext"/> without a logger.
    /// </summary>
    /// <param name="renameConfiguration"></param>
    /// <param name="toolingPaths"></param>
    public PackageEditorContext(PackageRenameConfiguration renameConfiguration, PackageNameData nameData, PackageToolingPaths toolingPaths)
        : this(renameConfiguration, nameData, toolingPaths, null)
    {
    }

    /// <summary>
    /// Creates a new <see cref="PackageEditorContext"/>.
    /// </summary>
    /// <param name="renameConfiguration"></param>
    /// <param name="toolingPaths"></param>
    /// <param name="logger"></param>
    public PackageEditorContext(PackageRenameConfiguration renameConfiguration, PackageNameData nameData, PackageToolingPaths toolingPaths, ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(renameConfiguration);
        ArgumentNullException.ThrowIfNull(nameData);
        ArgumentNullException.ThrowIfNull(toolingPaths);

        RenameConfiguration = renameConfiguration;
        _nameData = nameData;
        _toolingPaths = toolingPaths;
        _logger = MockLogger.MockIfNull(logger);
    }

    /// <summary>
    /// Sets a new <see cref="PackageRenameConfiguration"/>.
    /// </summary>
    /// <param name="config"></param>
    public void SetRenameConfiguration(PackageRenameConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        RenameConfiguration = config;
    }

    #region Editor Instance Creators

    /// <summary>
    /// Creates a new <see cref="PackageCompressor"/> using the current <see cref="PackageRenameConfiguration"/>.
    /// </summary>
    /// <returns></returns>
    public PackageCompressor CreatePackageCompressor()
    {
        ThrowIfNullConfig();

        CompressorConfiguration compressorConfig = RenameConfiguration!.CompressorConfiguration ?? new();

        return new PackageCompressor(compressorConfig, _toolingPaths, _nameData, _logger);
    }

    /// <summary>
    /// Creates a new <see cref="DirectoryEditor"/> using the current <see cref="PackageRenameConfiguration"/>.
    /// </summary>
    /// <returns></returns>
    public DirectoryEditor CreateDirectoryEditor()
    {
        ThrowIfNullConfig();

        DirectoryRenameConfiguration directoryConfig = RenameConfiguration!.DirectoryRenameConfiguration ?? new();
        directoryConfig.ApplyOverrides(RenameConfiguration!);

        return new DirectoryEditor(directoryConfig, _nameData, _logger)
            .SetReporter(_reporter);
    }

    public LibraryEditor CreateLibraryEditor()
    {
        ThrowIfNullConfig();

        LibraryRenameConfiguration libraryConfig = RenameConfiguration!.LibraryRenameConfiguration ?? new();
        libraryConfig.ApplyOverrides(RenameConfiguration!);

        return new LibraryEditor(libraryConfig, _nameData, _logger)
            .SetReporter(_reporter);
    }

    /// <summary>
    /// Creates a new <see cref="SmaliEditor"/> using the current <see cref="PackageRenameConfiguration"/>.
    /// </summary>
    /// <returns></returns>
    public SmaliEditor CreateSmaliEditor()
    {
        ThrowIfNullConfig();

        SmaliRenameConfiguration smaliConfig = RenameConfiguration!.SmaliRenameConfiguration ?? new();
        smaliConfig.ApplyOverrides(RenameConfiguration!);

        return new SmaliEditor(smaliConfig, _nameData, _logger)
            .SetReporter(_reporter);
    }

    /// <summary>
    /// Creates a new <see cref="AssetEditor"/> using the current <see cref="PackageRenameConfiguration"/>.
    /// </summary>
    /// <returns></returns>
    public AssetEditor CreateAssetEditor()
    {
        ThrowIfNullConfig();

        AssetRenameConfiguration assetConfig = RenameConfiguration!.AssetRenameConfiguration ?? new();
        assetConfig.ApplyOverrides(RenameConfiguration!);

        return new AssetEditor(assetConfig, _nameData, _logger)
            .SetReporter(_reporter);
    }

    #endregion Editor Instance Creators

    /// <summary>
    /// Assigns the package names to the shared <see cref="PackageNameData"/>. This is required in order for the rest of the renaming
    /// process to function.
    /// </summary>
    public void GatherPackageMetadata(string? manifestPath = null)
    {
        manifestPath = BaseRenameConfiguration.Coalesce(manifestPath, () => Path.Combine(_nameData.ApkAssemblyDirectory, "AndroidManifest.xml"));
        _nameData.OriginalPackageName = PackageCompressor.GetPackageName(manifestPath);

        (
            _,
            _nameData.OriginalCompanyName,
            _nameData.NewPackageName
        ) = PackageCompressor.SplitPackageName(_nameData);

        // Also set the output directory as long as a base directory is set and a explicit directory is not.
        if (_nameData.RenamedPackageOutputBaseDirectory is not null)
        {
            if (_nameData.RenamedPackageOutputDirectory is not null)
            {
                throw new InvalidConfigurationException("The RenamedPackageOutputDirectory must be null if RenamedPackageOutputBaseDirectory is set.");
            }

            _nameData.RenamedOutputDirectoryInternal = Path.Combine(_nameData.RenamedPackageOutputBaseDirectory, PackageUtils.GetFormattedTimeDirectory(_nameData.NewPackageName));
        }
        else
        {
            if (_nameData.RenamedPackageOutputDirectory is null)
            {
                throw new InvalidConfigurationException("Either RenamedPackageOutputBaseDirectory or RenamedPackageOutputDirectory must be set to valid paths.");
            }

            _nameData.RenamedOutputDirectoryInternal = _nameData.RenamedPackageOutputDirectory!;
        }

        _ = Directory.CreateDirectory(_nameData.RenamedOutputDirectoryInternal);

        RenameConfiguration?.BuildAndCacheRegex(_nameData.OriginalCompanyName);
    }

    public PackageEditorContext SetReporter(IProgress<ProgressInfo>? reporter)
    {
        _reporter = reporter;
        return this;
    }

    private void ThrowIfNullConfig()
    {
        ArgumentNullException.ThrowIfNull(RenameConfiguration);
    }
}
