using APKognito.ApkLib.Configuration;
using APKognito.ApkLib.Editors;
using APKognito.ApkLib.Interfaces;
using Microsoft.Extensions.Logging;

namespace APKognito.ApkLib;

public sealed class PackageEditorContext : IReportable<PackageEditorContext>//, ISettableNameDataContext<PackageEditorContext>
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
        ArgumentNullException.ThrowIfNull(toolingPaths);

        _logger = MockLogger.MockIfNull(logger);
        _toolingPaths = toolingPaths;
        RenameConfiguration = renameConfiguration;
        _nameData = nameData;
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
        return new PackageCompressor(_toolingPaths, _nameData, _logger);
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
