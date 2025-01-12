namespace APKognito.Configurations;

[Flags]
public enum ConfigModifiers
{
    None = 0,
    JsonIndented = 1,
    JsonIgnoreMissing = 2,
    MemoryPacked = 64,
    Compressed = 128,
}