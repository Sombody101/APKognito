namespace APKognito.Configurations;

/// <summary>
/// Tells <see cref="ConfigurationFactory"/> how to serialize and deserialize an inheritor of <see cref="IKognitoConfig"/>
/// </summary>
public enum ConfigType
{
    Json,
    MemoryPacked,
}

[Flags]
public enum ConfigModifiers
{
    None = 0,
    JsonIndented = 1,
    JsonIgnoreMissing = 2,
    MemoryPacked = 64,
    Compressed = 128,
}