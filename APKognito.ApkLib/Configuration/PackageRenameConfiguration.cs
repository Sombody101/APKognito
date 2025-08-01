namespace APKognito.ApkLib.Configuration;

public sealed record class PackageRenameConfiguration : BaseRenameConfiguration
{
    public PackageRenameConfiguration()
    {
        RegexTimeout = 60_000;
        InternalRenameInfoLogDelimiter = " to ";
    }

    // public int MaxThreads { get; init; }

    /// <summary>
    /// Usually used for debugging.
    /// Stops all temporary files from being deleted during the cleanup stage. This does not apply to temporary Smali files within the '<see langword="$(project)\$smali"/>' directory.
    /// </summary>
    public bool ClearTempFilesOnRename { get; init; }

    /// <summary>
    /// Configurations for the <see cref="Editors.PackageCompressor"/>
    /// </summary>
    public CompressorConfiguration CompressorConfiguration { get; set; }

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
    /// Optional override configurations for the <see cref="Editors.AssetEditor"/>.
    /// </summary>
    public AssetRenameConfiguration? AssetRenameConfiguration { get; init; }
}
