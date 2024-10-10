using APKognito.Utilities;
using MemoryPack;
using Newtonsoft.Json;
using System.IO;

namespace APKognito.Configurations;

public class ConfigurationFactory
{
    private readonly Dictionary<Type, IKognitoConfig> _cachedConfigs = [];

    /// <summary>
    /// Loads the given config type from file. If the file doesn't exist, a default config is returned and no file is created, edited, or destroyed.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T GetConfig<T>() where T : IKognitoConfig, new()
    {
        Type configType = typeof(T);

        if (_cachedConfigs.ContainsKey(configType))
        {
            return (T)_cachedConfigs[configType];
        }

        ConfigFileAttribute? configAttribute = GetConfigAttribute(configType);

        if (configAttribute is null)
        {
            var exception = new InvalidConfigModelException(configType);
            FileLogger.LogException(exception);
            throw exception;
        }

        FileLogger.Log($"Loading config file '{configAttribute.FileName}' for {configType.Name}");
        var config = LoadConfig<T>(configAttribute);
        _cachedConfigs[configType] = config;
        return config;
    }

    /// <summary>
    /// Saves the given configuration to a file, and adds it to the config cache if it wasn't there already.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="config"></param>
    public void SaveConfig(IKognitoConfig config)
    {
        var configAttribute = GetConfigAttribute(config.GetType())!;

        var fileName = configAttribute.FileName;

        FileLogger.Log($"Saving config file '{fileName}' for {config.GetType().Name}");

        switch (configAttribute.ConfigType)
        {
            case ConfigType.Json:
                Save_Json(config, fileName, configAttribute.ConfigModifier);
                break;

            case ConfigType.MemoryPacked:
                Save_MemoryPack(config, fileName);
                break;
        }

        // _cachedConfigs[typeof(T)] = config;
    }

    /// <summary>
    /// Saves all configs in the config cache to their respective files. Should only be called by <see cref="App.OnExit"/>
    /// </summary>
    public void SaveAllConfigs()
    {
        foreach (var config in _cachedConfigs.Select(cfg => cfg.Value))
        {
            SaveConfig(config);
        }
    }

    private T LoadConfig<T>(ConfigFileAttribute configAttribute) where T : IKognitoConfig, new()
    {
        string fileName = configAttribute.FileName;

        switch (configAttribute.ConfigType)
        {
            case ConfigType.Json:
                return Load_Json<T>(fileName, configAttribute.ConfigModifier) ?? new();

            case ConfigType.MemoryPacked:
                return Load_MemoryPack<T>(fileName) ?? new();
        }

        return new T();
    }

    private T? Load_MemoryPack<T>(string fileName) where T : IKognitoConfig, new()
    {
        string filePath = GetAppdataFile(fileName);

        if (!File.Exists(filePath))
        {
            return default;
        }

        byte[] loadedData = File.ReadAllBytes(filePath);

        T? loadedConfig = MemoryPackSerializer.Deserialize<T>(loadedData);

        return loadedConfig!;
    }

    private void Save_MemoryPack(IKognitoConfig config, string fileName)
    {
        string filePath = GetAppdataFile(fileName);

        Type type = config.GetType();

        byte[] packed = MemoryPackSerializer.Serialize(type, config);

        File.WriteAllBytes(filePath, packed);
    }

    private T? Load_Json<T>(string fileName, ConfigModifier modifier) where T : IKognitoConfig, new()
    {
        string filePath = GetAppdataFile(fileName);

        if (!File.Exists(filePath))
        {
            return default;
        }

        using StreamReader reader = new(filePath);
        using JsonTextReader jsonReader = new(reader);
        JsonSerializer serializer = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = modifier.HasFlag(ConfigModifier.JsonIgnoreMissing)
                ? MissingMemberHandling.Error
                : MissingMemberHandling.Ignore,
        };

        return serializer.Deserialize<T>(jsonReader)!;
    }

    private void Save_Json<T>(T config, string fileName, ConfigModifier modifier) where T : IKognitoConfig
    {
        using StreamWriter writer = new(GetAppdataFile(fileName));
        using JsonTextWriter jsonWriter = new(writer);
        JsonSerializer serializer = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = modifier.HasFlag(ConfigModifier.JsonIndented)
                ? Formatting.Indented
                : Formatting.None
        };

        serializer.Serialize(jsonWriter, config);
    }

    private static ConfigFileAttribute? GetConfigAttribute(Type configType)
    {
        return configType.GetCustomAttributes(typeof(ConfigFileAttribute), true)
            .Cast<ConfigFileAttribute>()
            .FirstOrDefault();
    }

    private static string GetAppdataFile(string filename)
    {
        string configs = Path.Combine(App.AppData!.FullName, "config");
        _ = Directory.CreateDirectory(configs);
        return Path.Combine(configs, filename);
    }
}
