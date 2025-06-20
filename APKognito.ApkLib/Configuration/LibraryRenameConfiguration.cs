namespace APKognito.ApkLib.Configuration;

public sealed record LibraryRenameConfiguration : BaseRenameConfiguration
{
    /// <summary>
    /// Disables renaming library files when set to <see langword="false"/>.
    /// </summary>
    public bool EnableLibraryRenaming { get; set; } = false;

    /// <summary>
    /// Disabled renaming library file names when set to <see langword="false"/>.
    /// </summary>
    public bool EnableLibraryFileRenaming { get; set; } = false;

    /// <summary>
    /// Extra files to force rename. These paths must be relative from the APK root. (i.g., "/AndroidManifest.xml" rather than "C:\...\AndroidManifest.xml")
    /// </summary>
    public List<ExtraPackageFile> ExtraInternalPackagePaths { get; init; } = [];
}
