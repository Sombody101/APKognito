using MemoryPack;

namespace APKognito.Configurations.ConfigModels;

[MemoryPackable]
[ConfigFile("misc.cache", ConfigType.MemoryPacked)]
internal partial class CacheStorage : IKognitoConfig
{
    /// <summary>
    /// Holds old APK paths to load (at least so there's content to present).
    /// </summary>
    public string? ApkSourcePath { get; set; }

    /// <summary>
    /// Specifies where to open the FileDialog to select an APK.
    /// </summary>
    public string? LastDialogDirectory { get; set; } = AppDomain.CurrentDomain.BaseDirectory;

    /// <summary>
    /// The newest update location (Stored here as a poor attempt of preventing user tampering)
    /// </summary>
    public string? UpdateSourceLocation { get; set; }

    public CacheStorage()
    {
    }

    [MemoryPackConstructor]
    private CacheStorage(string? apkSourcePath, string? lastDialogDirectory, string? updateSourceLocation)
    {
        ApkSourcePath = apkSourcePath;
        LastDialogDirectory = lastDialogDirectory;
        UpdateSourceLocation = updateSourceLocation;
    }
}