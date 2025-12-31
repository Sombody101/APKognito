using System.Diagnostics;
using APKognito.ApkLib.Configuration;
using APKognito.ApkLib.Editors;
using APKognito.ApkLib.Exceptions;
using Microsoft.Extensions.Logging;

namespace APKognito.ApkLib;

public sealed class PackageEditorContext
{
    internal PackageRenameConfiguration _renameConfiguration;
    private readonly PackageToolingPaths _toolingPaths;

    private readonly ILogger _logger;
    private readonly IProgress<ProgressInfo>? _reporter;

    internal readonly PackageRenameState _nameState;

    /// <summary>
    /// Creates a new <see cref="PackageEditorContext"/> without a logger.
    /// </summary>
    /// <param name="renameConfiguration"></param>
    /// <param name="toolingPaths"></param>
    public PackageEditorContext(PackageRenameConfiguration renameConfiguration, PackageToolingPaths toolingPaths, PackageRenameState nameState)
        : this(renameConfiguration, toolingPaths, nameState, null, null)
    {
    }

    /// <summary>
    /// Creates a new <see cref="PackageEditorContext"/>.
    /// </summary>
    /// <param name="renameConfiguration"></param>
    /// <param name="toolingPaths"></param>
    /// <param name="logger"></param>
    public PackageEditorContext(PackageRenameConfiguration renameConfiguration, PackageToolingPaths toolingPaths, PackageRenameState nameState, ILogger? logger, IProgress<ProgressInfo>? reporter)
    {
        ArgumentNullException.ThrowIfNull(renameConfiguration);
        ArgumentNullException.ThrowIfNull(toolingPaths);

        _renameConfiguration = renameConfiguration;
        _toolingPaths = toolingPaths;
        _logger = MockLogger.MockIfNull(logger);
        _reporter = reporter;
        _nameState = nameState;
    }

    /// <summary>
    /// Sets a new <see cref="PackageRenameConfiguration"/>.
    /// </summary>
    /// <param name="config"></param>
    public void SetRenameConfiguration(PackageRenameConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _renameConfiguration = config;
    }

    #region Editor Instance Creators

    /// <summary>
    /// Creates a new <see cref="PackageCompressor"/> using the current <see cref="PackageRenameConfiguration"/>.
    /// </summary>
    /// <returns></returns>
    public PackageCompressor CreatePackageCompressor()
    {
        ThrowIfNullConfig();

        CompressorConfiguration compressorConfig = _renameConfiguration!.CompressorConfiguration ?? new();

        return new PackageCompressor(compressorConfig, _toolingPaths, _nameState, _logger, _reporter);
    }

    /// <summary>
    /// Creates a new <see cref="DirectoryEditor"/> using the current <see cref="PackageRenameConfiguration"/>.
    /// </summary>
    /// <returns></returns>
    public DirectoryEditor CreateDirectoryEditor()
    {
        ThrowIfNullConfig();

        DirectoryRenameConfiguration directoryConfig = _renameConfiguration!.DirectoryRenameConfiguration ?? new();
        directoryConfig.ApplyOverrides(_renameConfiguration!);

        return new DirectoryEditor(directoryConfig, _nameState, _logger, _reporter);
    }

    public LibraryEditor CreateLibraryEditor()
    {
        ThrowIfNullConfig();

        LibraryRenameConfiguration libraryConfig = _renameConfiguration!.LibraryRenameConfiguration ?? new();
        libraryConfig.ApplyOverrides(_renameConfiguration!);

        return new LibraryEditor(libraryConfig, _nameState, _logger, _reporter);
    }

    /// <summary>
    /// Creates a new <see cref="SmaliEditor"/> using the current <see cref="PackageRenameConfiguration"/>.
    /// </summary>
    /// <returns></returns>
    public SmaliEditor CreateSmaliEditor()
    {
        ThrowIfNullConfig();

        SmaliRenameConfiguration smaliConfig = _renameConfiguration!.SmaliRenameConfiguration ?? new();
        smaliConfig.ApplyOverrides(_renameConfiguration!);

        return new SmaliEditor(smaliConfig, _nameState, _logger, _reporter);
    }

    /// <summary>
    /// Creates a new <see cref="PackageBootstrapper"/> using the current <see cref="PackageRenameConfiguration"/>.
    /// </summary>
    /// <returns></returns>
    public PackageBootstrapper CreatePackageBootstrapper()
    {
        ThrowIfNullConfig();
        ArgumentNullException.ThrowIfNull(_renameConfiguration!.BootstrapConfiguration);

        BootstrapConfiguration bootstrapConfig = _renameConfiguration.BootstrapConfiguration;

        return new PackageBootstrapper(_nameState.SourcePackagePath, bootstrapConfig, _logger);
    }

    /// <summary>
    /// Creates a new <see cref="AssetEditor"/> using the current <see cref="PackageRenameConfiguration"/>.
    /// </summary>
    /// <returns></returns>
    public AssetEditor CreateAssetEditor()
    {
        ThrowIfNullConfig();

        AssetRenameConfiguration assetConfig = _renameConfiguration.AssetRenameConfiguration ?? new();
        assetConfig.ApplyOverrides(_renameConfiguration);

        return new AssetEditor(assetConfig, _nameState, _logger, _reporter);
    }

    #endregion Editor Instance Creators

    public void GatherPackageMetadata(string? manifestPath = null)
    {
        manifestPath = BaseRenameConfiguration.Coalesce(manifestPath, () => Path.Combine(_nameState.PackageAssemblyDirectory, "AndroidManifest.xml"));
        _nameState.OldPackageName = PackageCompressor.GetPackageName(manifestPath);

        (_nameState.OldCompanyName, _nameState.NewPackageName) = PackageCompressor.SplitPackageName(_nameState);

        if (_renameConfiguration.CreateOutputSubdirectory)
        {
            _nameState.PackageOutputDirectory = Path.Combine(_nameState.PackageOutputDirectory, PackageUtils.GetFormattedTimeDirectory(_nameState.NewPackageName));
        }

        _ = Directory.CreateDirectory(_nameState.PackageOutputDirectory);

        if (_renameConfiguration.UseBootstrapClassLoader)
        {
            InvalidConfigurationException.ThrowIfNull(_renameConfiguration.BootstrapConfiguration);

            string oldAppName = _nameState.OldPackageName.Split('.')[^1]; // If this fails, the package was already fucked (or my code that gathers it lol)
            _nameState.NewPackageName = _renameConfiguration.BootstrapConfiguration.NewPackageName.Replace("{appname}", oldAppName, StringComparison.OrdinalIgnoreCase);
            _renameConfiguration.BootstrapConfiguration.NewPackageName = _nameState.NewPackageName;

            _renameConfiguration.InternalRenameRegexString = _nameState.OldPackageName.Replace(".", "\\.");
            _renameConfiguration.BuildAndCacheRegex(string.Empty);
        }
        else
        {
            _renameConfiguration.BuildAndCacheRegex(_nameState.OldCompanyName);
        }
    }

    [DebuggerHidden]
    private void ThrowIfNullConfig()
    {
        ArgumentNullException.ThrowIfNull(_renameConfiguration);
    }
}
