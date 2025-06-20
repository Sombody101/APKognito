namespace APKognito.ApkLib.Configuration;

public record PackageNameData
{
    // TODO: Implement setters for editors
    // Will that make it harder to use each editor without the main editor context? Might need to add override parameters for each method that uses namedata.

    /// <summary>
    /// The replacement package company name. (Passed from caller)
    /// </summary>
    public required string NewCompanyName { get; set; } = string.Empty;

    /// <summary>
    /// The full path to the source APK file.
    /// </summary>
    public required string FullSourceApkPath { get; set; } = string.Empty;

    /// <summary>
    /// The full name for the source APK file.
    /// </summary>
    public required string FullSourceApkFileName { get; set; } = string.Empty;

    /// <summary>
    /// The temporary directory that the unpacked APK is placed.
    /// </summary>
    public required string ApkAssemblyDirectory { get; set; } = string.Empty;

    /// <summary>
    /// The directory the renamed package will be placed into. If you want ApkLib to create a subdirectory for the output,
    /// then set <see cref="RenamedPackageOutputBaseDirectory"/> rather than this property.
    /// </summary>
    public string? RenamedPackageOutputDirectory { get; set; }

    /// <summary>
    /// The base directory for a renamed package directory to be placed into. If you want to set the explicit directory that
    /// a renamed package will be placed into, use <see cref="RenamedPackageOutputDirectory"/>. This property will make ApkLib to create a directory
    /// inside the given path and place the renamed package into that.
    /// </summary>
    public string? RenamedPackageOutputBaseDirectory { get; set; }

    /// <summary>
    /// The original full package name, fetched from the AndroidManifest.xml of the APK.
    /// </summary>
    public string OriginalPackageName { get; internal set; } = string.Empty;

    /// <summary>
    /// The original company name, extracted from <see cref="OriginalPackageName"/>.
    /// </summary>
    public string OriginalCompanyName { get; internal set; } = string.Empty;

    /// <summary>
    /// The new fully-formatted package name, using <see cref="NewCompanyName"/>.
    /// </summary>
    public string NewPackageName { get; internal set; } = string.Empty;

    internal string RenamedOutputDirectoryInternal { get; set; } = string.Empty;

    /// <summary>
    /// A sub-directory in side of <see cref="ApkAssemblyDirectory"/> for replacing company name instances.
    /// (Only used when file is larger than <see cref="MAX_SMALI_LOAD_SIZE"/>)
    /// </summary>
    public required string ApkSmaliTempDirectory { get; set; } = string.Empty;

    public string FinalOutputDirectory => RenamedOutputDirectoryInternal;

    public static string SubtractPathFrom(string path, string subtractor)
    {
        return path.StartsWith(subtractor)
            ? path[subtractor.Length..]
            : path;
    }
}
