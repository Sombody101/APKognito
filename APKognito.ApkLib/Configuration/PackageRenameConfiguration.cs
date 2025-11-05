namespace APKognito.ApkLib.Configuration;

public sealed record PackageRenameConfiguration : BaseRenameConfiguration
{
    public PackageRenameConfiguration()
    {
        RegexTimeout = 60_000;
        InternalRenameInfoLogDelimiter = " to ";
    }

    /// <summary>
    /// Usually used for debugging.
    /// Stops all temporary files from being deleted during the cleanup stage. This does not apply to temporary Smali files within the '<see langword="$(project)\$smali"/>' directory.
    /// </summary>
    public bool ClearTempFilesOnRename { get; init; }

    /// <summary>
    /// Injects a bootstrapper activity. Significantly reduces rename time and allows for
    /// more customization of the resulting package name.
    ///
    /// <para>
    /// When enabled, the following configurations will be ignored:
    /// <list type="bullet">
    /// <item>
    /// <see cref="DirectoryRenameConfiguration"/>
    /// </item>
    /// <item>
    /// <see cref="LibraryRenameConfiguration"/>
    /// </item>
    /// <item>
    /// <see cref="SmaliRenameConfiguration"/>
    /// </item>
    /// </list>
    /// </para>
    /// </summary>
    public bool UseBootstrapClassLoader { get; init; } = false;

    /// <summary>
    /// Creates a subdirectory inside of <see cref="PackageOutputDirectory"/> for the renamed package using the date as the name.
    /// </summary>
    public bool CreateOutputSubdirectory { get; set; } = true;

    /// <summary>
    /// Configurations for the <see cref="Editors.PackageCompressor"/>
    /// </summary>
    public CompressorConfiguration? CompressorConfiguration { get; set; }

    /// <summary>
    /// Optional override configurations for the <see cref="Editors.DirectoryEditor"/>.
    /// </summary>
    public DirectoryRenameConfiguration? DirectoryRenameConfiguration { get; init; }

    /// <summary>
    /// Optional override configurations for the <see cref="Editors.LibraryEditor"/>.
    /// </summary>
    public LibraryRenameConfiguration? LibraryRenameConfiguration { get; init; }

    /// <summary>
    /// Optional override configurations for the <see cref="Editors.SmaliEditor"/>.
    /// </summary>
    public SmaliRenameConfiguration? SmaliRenameConfiguration { get; init; }

    /// <summary>
    /// Options for a the package bootstrapper.
    /// This is only used when <see cref="UseBootstrapClassLoader"/> is enabled.
    /// </summary>
    public BootstrapConfiguration? BootstrapConfiguration { get; init; }

    /// <summary>
    /// Optional override configurations for the <see cref="Editors.AssetEditor"/>.
    /// </summary>
    public AssetRenameConfiguration? AssetRenameConfiguration { get; init; }
}
