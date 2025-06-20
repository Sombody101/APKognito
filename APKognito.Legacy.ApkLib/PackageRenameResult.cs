namespace APKognito.Legacy.ApkLib;

public class PackageRenameResult
{
    public string ResultStatus { get; init; } = string.Empty;

    public bool Successful { get; init; }

    public required RenameOutputLocations OutputLocations { get; init; }
}

public class RenameOutputLocations
{
    public string? AssetPath { get; }

    public string OutputApkPath { get; }

    public RenameOutputLocations(string? assetPath, string outputApkPath)
    {
        AssetPath = assetPath;
        OutputApkPath = outputApkPath;
    }
}
