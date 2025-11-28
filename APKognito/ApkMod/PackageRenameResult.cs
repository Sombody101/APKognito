namespace APKognito.ApkMod;

public sealed record PackageRenameResult
{
    public string ResultStatus { get; init; } = string.Empty;

    public bool Successful { get; init; }

    public required RenameOutputLocations OutputLocations { get; init; }

    public RenamedPackageMetadata? RenamedPackageMetadata { get; set; }
}

public sealed record RenameOutputLocations
{
    public string OutputApkPath { get; }

    public string? AssetsDirectory { get; }

    public string NewPackageName { get; }

    public string OriginalPackageName { get; }

    public RenameOutputLocations(string outputApkPath, string? assetsDirectory, string newPackageName, string originalPackageName)
    {
        OutputApkPath = outputApkPath;
        AssetsDirectory = assetsDirectory;
        NewPackageName = newPackageName;
        OriginalPackageName = originalPackageName;
    }

    public static RenameOutputLocations Empty => new(null!, null, string.Empty, string.Empty);
}
