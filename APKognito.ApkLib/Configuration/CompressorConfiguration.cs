namespace APKognito.ApkLib.Configuration;

public sealed record CompressorConfiguration : BaseRenameConfiguration
{
    /// <summary>
    /// Extra options to pass to the Java executable.
    /// </summary>
    public IEnumerable<string> ExtraJavaOptions { get; init; } = [];
}
