using APKognito.Utilities;
using MemoryPack;
using Newtonsoft.Json;
using System.IO;

namespace APKognito.Configurations;

public static class ConfigurationFactory
{
    private static readonly Dictionary<Type, IKognitoConfig> _cachedConfigs = [];

    /// <summary>
    /// Loads the given config type from file. If the file doesn't exist, a default config is returned and no file is created, edited, or destroyed.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T GetConfig<T>() where T : IKognitoConfig, new()
    {
        Type configType = typeof(T);

        if (_cachedConfigs.ContainsKey(configType))
        {
            FileLogger.Log($"Fetch cached {configType.Name}");
            return (T)_cachedConfigs[configType];
        }

        ConfigFileAttribute? configAttribute = GetConfigAttribute(configType);

        if (configAttribute is null)
        {
            var exception = new InvalidConfigModelException(configType);
            FileLogger.LogException(exception);
            throw exception;
        }

        FileLogger.Log($"'{configAttribute.FileName}' for {configType.Name}, caching");
        var config = LoadConfig<T>(configAttribute);
        _cachedConfigs[configType] = config;
        return config;
    }

    /// <summary>
    /// Get the <see cref="ConfigFileAttribute"/> for the given config <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static ConfigFileAttribute GetConfigInfo<T>()
    {
        return GetConfigAttribute(typeof(T));
    }

    /// <summary>
    /// Saves the given configuration to it's respective file.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="config"></param>
    public static void SaveConfig(IKognitoConfig config)
    {
        var configAttribute = GetConfigAttribute(config.GetType())!;

        string filePath = configAttribute.FileName;

        if (!configAttribute.LoadedFromCurrentDirectory)
        {
            FileLogger.Log($"'{filePath}' for {config.GetType().Name}");
            filePath = configAttribute.CompletePath();
        }
        else
        {
            FileLogger.Log($"Sv CurDir file: ./{filePath}");

        }

        switch (configAttribute.ConfigType)
        {
            case ConfigType.Json:
                Save_Json(config, filePath, configAttribute.ConfigModifier);
                break;

            case ConfigType.MemoryPacked:
                Save_MemoryPack(config, filePath);
                break;
        }
    }

    /// <summary>
    /// Saves all configs in the config cache to their respective files. Should only be called by <see cref="App.OnExit"/>
    /// </summary>
    public static void SaveAllConfigs()
    {
        foreach (var config in _cachedConfigs.Select(cfg => cfg.Value))
        {
            SaveConfig(config);
        }
    }

    private static T LoadConfig<T>(ConfigFileAttribute configAttribute) where T : IKognitoConfig, new()
    {
        string filePath = configAttribute.FileName;

        // Check if the file was moved into the same directory as the app
        if (!File.Exists(filePath))
        {
            filePath = configAttribute.CompletePath();
        }
        else
        {
            FileLogger.Log($"Ld CurDir file: ./{filePath}");
            configAttribute.LoadedFromCurrent();
        }

        switch (configAttribute.ConfigType)
        {
            case ConfigType.Json:
                return Load_Json<T>(filePath, configAttribute.ConfigModifier) ?? new();

            case ConfigType.MemoryPacked:
                return Load_MemoryPack<T>(filePath) ?? new();
        }

        return new T();
    }

    private static T? Load_MemoryPack<T>(string filePath) where T : IKognitoConfig, new()
    {
        if (!File.Exists(filePath))
        {
            return default;
        }

        byte[] loadedData = File.ReadAllBytes(filePath);

        T? loadedConfig = MemoryPackSerializer.Deserialize<T>(loadedData);

        return loadedConfig!;
    }

    private static void Save_MemoryPack(IKognitoConfig config, string filePath)
    {
        Type type = config.GetType();

        byte[] packed = MemoryPackSerializer.Serialize(type, config);

        File.WriteAllBytes(filePath, packed);
    }

    private static T? Load_Json<T>(string filePath, ConfigModifier modifier) where T : IKognitoConfig, new()
    {
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
                ? MissingMemberHandling.Ignore
                : MissingMemberHandling.Error,
        };

        return serializer.Deserialize<T>(jsonReader)!;
    }

    private static void Save_Json<T>(T config, string filePath, ConfigModifier modifier) where T : IKognitoConfig
    {
        using StreamWriter writer = new(filePath);
        using JsonTextWriter jsonWriter = new(writer);
        JsonSerializer serializer = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = modifier.HasFlag(ConfigModifier.JsonIndented)
                ? Formatting.Indented
                : Formatting.None,
        };

        serializer.Serialize(jsonWriter, config);
    }

    private static ConfigFileAttribute GetConfigAttribute(Type configType)
    {
        return configType.GetCustomAttributes(typeof(ConfigFileAttribute), true)
            .Cast<ConfigFileAttribute>()
            .First();
    }
}