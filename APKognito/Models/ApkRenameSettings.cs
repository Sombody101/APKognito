namespace APKognito.Models;

public sealed class ApkRenameSettings
{
    public string SourceApkPath { get; set; } = string.Empty;

    public string OutputDirectory { get; init; } = string.Empty;
    public string JavaPath { get; init; } = string.Empty;
    public string TempDirectory { get; init; } = string.Empty;
    public string ApkReplacementName { get; init; } = string.Empty;

    public Action<string>? OnPackageNameFound { get; init; }
}
