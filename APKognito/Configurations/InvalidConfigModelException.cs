namespace APKognito.Configurations;

public class InvalidConfigModelException : Exception
{
    public InvalidConfigModelException(Type configType)
        : base($"The config model {configType.Name} does not implement {nameof(ConfigFileAttribute)}")
    {
    }
}