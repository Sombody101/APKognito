using APKognito.ApkLib.Configuration;
using APKognito.Configurations.ConfigModels;

namespace APKognito.ApkMod;

public record RenameConfiguration
{
    public required Configurations.ConfigModels.UserRenameConfiguration KognitoConfig { get; set; }

    public required AdvancedApkRenameSettings AdvancedConfig { get; set; }

    public required PackageToolingPaths ToolingPaths { get; set; }

    public string SourcePackagePath { get; set; } = string.Empty;

    public string OutputBaseDirectory { get; set; } = string.Empty;

    public string TempDirectory { get; set; } = string.Empty;

    public string ReplacementCompanyName { get; set; } = string.Empty;
}
