using MemoryPack;

namespace APKognito.Configurations.ConfigModels;

[MemoryPackable]
[ConfigFile("misc.cache", ConfigType.MemoryPacked)]
internal partial class CacheItems : IKognitoConfig
{
    /// <summary>
    /// Holds old APK paths to load (at least so there's content to present).
    /// </summary>
    public string? ApkSourcePath { get; set; }

    /// <summary>
    /// Specifies where to open the FileDialog to select an APK.
    /// </summary>
    public string? LastDialogDirectory { get; set; } = AppDomain.CurrentDomain.BaseDirectory;

    public CacheItems()
    {
    }

    [MemoryPackConstructor]
    private CacheItems(string? apkSourcePath, string? lastDialogDirectory)
    {
        ApkSourcePath = apkSourcePath;
        LastDialogDirectory = lastDialogDirectory;
    }
}