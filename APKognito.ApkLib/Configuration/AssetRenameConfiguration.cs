namespace APKognito.ApkLib.Configuration;

public record AssetRenameConfiguration : BaseRenameConfiguration
{
    /// <summary>
    /// The directory containing all assets (OBB, bundle, etc).
    /// Leave <see langword="null"/> if the assets directory is next to the source package within the same directory.
    /// </summary>
    public string? AssetDirectory { get; init; }

    /// <summary>
    /// Specifies that asset files should be copied to the output location rather than being moved.
    /// This should only be set to <see langword="false"/> if drive usage is a concern.
    /// </summary>
    public bool CopyAssets { get; init; } = true;

    /// <summary>
    /// Reads through all the entries of a valid OBB archive and renames assets where needed.
    /// This only applies to archives, asset bundles under the name of an OBB.
    /// </summary>
    public bool RenameObbArchiveEntries { get; init; } = true;

    /// <summary>
    /// Extra files within OBB archives.
    /// </summary>
    public IReadOnlyCollection<string> RenameObbsInternalExtras { get; init; } = [];

    /// <summary>
    /// Extra files to force rename. These paths must be absolute from the APK root. (i.g., "/AndroidManifest.xml" rather than "C:\...\AndroidManifest.xml")
    /// </summary>
    public string[] ExtraInternalPackagePaths { get; init; } = [];

    /// <summary>
    /// The buffer size when copying assets. Defaults to 128KB.
    /// </summary>
    public int AssetCopyBuffer { get; init; } = 1024 * 128;
}
