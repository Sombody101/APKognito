namespace APKognito.ApkMod;

public class PackageRenameResult
{
    public string ResultStatus { get; init; } = string.Empty;

    public bool Successful { get; init; }

    public required RenameOutputLocations OutputLocations { get; init; }
}

public class RenameOutputLocations
{
    public string OutputApkPath { get; }

    public string? AssetsDirectory { get; }

    public string NewPackageName { get; }

    public RenameOutputLocations(string outputApkPath, string? assetsDirectory, string newPackageName)
    {
        OutputApkPath = outputApkPath;
        AssetsDirectory = assetsDirectory;
        NewPackageName = newPackageName;
    }
}
