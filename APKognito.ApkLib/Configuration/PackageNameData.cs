namespace APKognito.ApkLib.Configuration;

public record PackageRenameState
{
    public required string SourcePackagePath { get; set; }
    public required string NewCompanyName { get; set; }
    public string NewPackageName { get; set; }

    public string? OldPackageName { get; internal set; }
    public string? OldCompanyName { get; internal set; }

    public required string PackageOutputDirectory { get; set; }
    public required string PackageAssemblyDirectory { get; set; }
    public required string? SmaliAssemblyDirectory { get; set; }

    public (string oldName, string newName) GetPackageRenamePair(bool useBootstrapper)
    {
        return useBootstrapper
            ? (OldPackageName!, NewPackageName)
            : (OldCompanyName!, NewCompanyName);
    }

    public static string SubtractPathFrom(string path, string subtractor)
    {
        return path.StartsWith(subtractor)
            ? path[subtractor.Length..]
            : path;
    }

    public static readonly PackageRenameState Empty = new()
    {
        PackageAssemblyDirectory = string.Empty,
        SmaliAssemblyDirectory = string.Empty,
        SourcePackagePath = string.Empty,
        NewCompanyName = string.Empty,
        PackageOutputDirectory = string.Empty,
    };
}
