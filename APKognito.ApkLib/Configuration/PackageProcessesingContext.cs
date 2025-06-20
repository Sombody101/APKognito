namespace APKognito.ApkLib.Configuration;

public record ApkProcessingContext
{
    /// <summary>
    /// The internal package name (e.g., "com.example.app") discovered from the AndroidManifest.xml.
    /// This is populated after the APK is unpacked.
    /// </summary>
    public string? FullPackageName { get; internal set; }

    /// <summary>
    /// The full package name with the company name replaced.
    /// </summary>
    public string? FullReplacementPackageName { get; internal set; }

    /// <summary>
    /// The original company name derived from the package name (if applicable).
    /// </summary>
    public string? OriginalCompanyName { get; internal set; }

    /// <summary>
    /// The replacement company name provided by the user.
    /// </summary>
    public string? ReplacementCompanyName { get; internal set; }
}
