namespace APKognito.Configurations.ConfigModels;

[ConfigFile("adb-history.bin", ConfigType.MemoryPacked, ConfigModifiers.MemoryPacked | ConfigModifiers.Compressed)]
internal class AdbHistory : IKognitoConfig
{
    public List<string> CommandHistory { get; set; } = [];
    public List<string> OutputHistory { get; set; } = [];
}
