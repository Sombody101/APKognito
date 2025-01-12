namespace APKognito.Configurations;

/// <summary>
/// Tells <see cref="ConfigurationFactory"/> how to serialize and deserialize an inheritor of <see cref="IKognitoConfig"/>
/// </summary>
public enum ConfigType
{
    Json,
    MemoryPacked,
}