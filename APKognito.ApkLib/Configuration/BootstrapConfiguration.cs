namespace APKognito.ApkLib.Configuration;

public record BootstrapConfiguration
{
    /// <summary>
    /// The new package name.
    /// </summary>
    public required string NewPackageName { get; set; }

    /// <summary>
    /// An optional name to display in error reporting dialogs.
    /// The name will be extracted from the package manifest if not provided.
    /// </summary>
    public string? FriendlyAppName { get; set; }

    /// <summary>
    /// Enable an error reporting window in the bootstrapper.
    /// </summary>
    public bool EnableErrorReporting { get; init; } = true;
}
