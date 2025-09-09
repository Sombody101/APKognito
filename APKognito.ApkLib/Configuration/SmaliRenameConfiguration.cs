namespace APKognito.ApkLib.Configuration;

public sealed record SmaliRenameConfiguration : BaseRenameConfiguration
{
    /// <summary>
    /// Extra files to force rename. These paths must be absolute from the APK root. (i.g., "/AndroidManifest.xml" rather than "C:\...\AndroidManifest.xml")
    /// </summary>
    public List<ExtraPackageFile> ExtraInternalPackagePaths { get; init; } = [];

    /// <summary>
    /// Scans every Smali file for the new company name. If it's not found, the file is skipped.
    /// This option can also work as a "potato mode" as the scan is single threaded, so the amount of work per second
    /// and overall hardware usage is greatly decreased. (including driver calls)
    /// </summary>
    public bool ScanSmaliBeforeReplace { get; set; } = false;

    /// <summary>
    /// The number of bytes a Smali file can be before it's opened as a stream rather than copying all of its content into memory.
    /// The defautl size is 1MB (1,048,576 bytes)
    /// </summary>
    public int MaxSmaliLoadSize { get; init; } = 1024 * 1024;

    /// <summary>
    /// The buffer size for each Smali file and defaults to 64KB to reduce the number of drivers calls.
    /// This helps best with large packages, but is negligible on smaller packages.
    /// </summary>
    public int SmaliBufferSize { get; init; } = 1024 * 64;
}
