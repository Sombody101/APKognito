namespace APKognito.ApkLib.Configuration;

public sealed record SmaliRenameConfiguration : BaseRenameConfiguration
{
    /// <summary>
    /// Extra files to force rename. These paths must be absolute from the APK root. (i.g., "/AndroidManifest.xml" rather than "C:\...\AndroidManifest.xml")
    /// </summary>
    public List<ExtraPackageFile> ExtraInternalPackagePaths { get; init; } = [];

    /// <summary>
    /// The number of bytes a Smali file can be before it's opened as a stream rather than copying all of its content into memory.
    /// The defautl size is 20KB (20,248 bytes)
    /// </summary>
    public int MaxSmaliLoadSize { get; init; } = 1024 * 20;
}
