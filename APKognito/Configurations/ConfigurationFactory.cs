using System.IO;
using System.IO.Compression;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using APKognito.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace APKognito.Configurations;

public class ConfigurationFactory
{
    public string ConfigurationDirectory { get; } = Path.Combine(App.AppDataDirectory.FullName, "config");

    // Keeps the configs "secure" (classes will still have references to each instance, but this ensures there can only be one instance in use at a time)
    private readonly Dictionary<Type, IKognitoConfig> _cachedConfigs = [];
    private readonly Dictionary<Type, ConfigFileAttribute> _cachedAttributes = [];

    public ConfigurationFactory()
    {
        _ = Directory.CreateDirectory(ConfigurationDirectory);
    }

    /// <summary>
    /// Loads the given config type from file. If the file doesn't exist, a default config is returned and no file is created, edited, or destroyed.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T GetConfig<T>(bool forceReload = false) where T : class, IKognitoConfig, new()
    {
        Type configType = typeof(T);

        // Return a cached config
        if (!forceReload && _cachedConfigs.TryGetValue(configType, out IKognitoConfig? value))
        {
            FileLogger.LogDebug($"Fetch cached {configType.Name}");
            return (T)value;
        }

        // Get the config attributes for the model, or create one
        ConfigFileAttribute? configAttribute = GetConfigAttribute(configType);

        if (configAttribute is null)
        {
            InvalidConfigModelException exception = new(configType);
            FileLogger.LogException(exception);
            throw exception;
        }

        FileLogger.LogDebug($"'{configAttribute.FileName}' for {configType.Name}, caching");
        T config = LoadConfig<T>(configAttribute);
        _cachedConfigs[configType] = config;

        return config;
    }

    /// <summary>
    /// Attempts to get a cached config. Returns <see langword="null"/> if the wanted config is not cached.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="config"></param>
    /// <returns></returns>
    public bool TryGetConfig<T>(out T? config) where T : class, IKognitoConfig, new()
    {
        if (_cachedConfigs.TryGetValue(typeof(T), out IKognitoConfig? fetchedConfig))
        {
            config = (T)fetchedConfig!;
            return true;
        }

        config = null;
        return false;
    }

    /// <summary>
    /// Get the <see cref="ConfigFileAttribute"/> for the given config <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public ConfigFileAttribute GetConfigInfo<T>()
    {
        return GetConfigAttribute(typeof(T));
    }

    /// <summary>
    /// Saves the given configuration to its respective file.
    /// </summary>
    /// <param name="config"></param>
    public void SaveConfig(IKognitoConfig config, [Optional] ConfigFileAttribute? configAttribute)
    {
        configAttribute ??= GetConfigAttribute(config.GetType())!;

        string filePath = GetCompletePath(configAttribute.FileName);

        FileLogger.Log($"'{filePath}' for {config.GetType().Name}");

        switch (configAttribute.ConfigType)
        {
            case ConfigType.Json:
                Save_Json(config, filePath, configAttribute.ConfigModifiers);
                break;

            case ConfigType.Bson:
                Save_Bson(config, filePath, configAttribute.ConfigModifiers);
                break;

            default:
                throw new UnknownConfigTypeException(configAttribute.ConfigType);
        }
    }

    public void SaveConfigs(params IKognitoConfig[] configs)
    {
        foreach (IKognitoConfig config in configs)
        {
            SaveConfig(config);
        }
    }

    public void SaveConfig<T>()
    {
        Type passedType = typeof(T);

        if (!_cachedConfigs.TryGetValue(passedType, out IKognitoConfig? config))
        {
            SaveConfig(config!);
        }
    }

    /// <summary>
    /// Saves all configs in the config cache to their respective files. Should only be called by <see cref="App.OnExitAsync"/>
    /// </summary>
    public void SaveAllConfigs()
    {
        foreach (IKognitoConfig? config in _cachedConfigs.Select(cfg => cfg.Value))
        {
            SaveConfig(config);
        }
    }

    /// <summary>
    /// Loads an arbitrary config file without caching it. (no singleton config)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T LoadFile<T>() where T : IKognitoConfig, new()
    {
        Type configType = typeof(T);

        ConfigFileAttribute? configAttribute = GetNoCacheConfigAttribute(configType);

        if (configAttribute is null)
        {
            InvalidConfigModelException exception = new(configType);
            FileLogger.LogException(exception);
            throw exception;
        }

        FileLogger.Log($"'{configAttribute.FileName}' for {configType.Name}, caching");
        T config = LoadConfig<T>(configAttribute);

        return config;
    }

    private T LoadConfig<T>(ConfigFileAttribute configAttribute) where T : IKognitoConfig, new()
    {
        string configPath = GetCompletePath(configAttribute.FileName);

        // Load methods will return a default instance of each config type if their file is not found.
        // That way all configs are only stored in memory until it's time to save them, at which point they're serialized to
        // their respective files.
        return configAttribute.ConfigType switch
        {
            ConfigType.Json => Load_Json<T>(configPath, configAttribute.ConfigModifiers) ?? new(),
            ConfigType.Bson => Load_Bson<T>(configPath, configAttribute.ConfigModifiers) ?? new(),
            // ConfigType.MemoryPacked => Load_MemoryPack<T>(configPath, configAttribute.ConfigModifier) ?? new(),
            _ => throw new UnknownConfigTypeException(configAttribute.ConfigType),
        };
    }

    private static T? Load_Json<T>(string filePath, ConfigModifiers modifiers) where T : IKognitoConfig, new()
    {
        if (!File.Exists(filePath))
        {
            return new();
        }

        try
        {
            using StreamReader reader = new(filePath);
            using JsonTextReader jsonReader = new(reader);
            JsonSerializer serializer = new()
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = modifiers.HasFlag(ConfigModifiers.JsonIgnoreMissing)
                    ? MissingMemberHandling.Ignore
                    : MissingMemberHandling.Error,
            };

            return serializer.Deserialize<T>(jsonReader)!;
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            return new();
        }

    }

    private static void Save_Json<T>(T config, string filePath, ConfigModifiers modifier) where T : IKognitoConfig
    {
        using StreamWriter writer = new(filePath);
        using JsonTextWriter jsonWriter = new(writer);
        JsonSerializer serializer = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = modifier.HasFlag(ConfigModifiers.JsonIndented)
                ? Formatting.Indented
                : Formatting.None,
        };

        serializer.Serialize(jsonWriter, config);
    }

    private static T? Load_Bson<T>(string filePath, ConfigModifiers modifiers) where T : IKognitoConfig, new()
    {
        if (!File.Exists(filePath))
        {
            return new();
        }

        try
        {
            byte[] data = File.ReadAllBytes(filePath);

            if (modifiers.HasFlag(ConfigModifiers.Compressed))
            {
                data = Unzip(data);
            }

            using MemoryStream stream = new(data);
            using BsonDataReader reader = new(stream);

            var deserializer = new JsonSerializer();
            return deserializer.Deserialize<T>(reader);
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            return new();
        }
    }

    private static void Save_Bson<T>(T config, string filePath, ConfigModifiers modifiers) where T : IKognitoConfig
    {
        using MemoryStream ms = new();
        using BsonDataWriter writer = new(ms);

        JsonSerializer serializer = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = modifiers.HasFlag(ConfigModifiers.JsonIgnoreMissing)
                ? MissingMemberHandling.Ignore
                : MissingMemberHandling.Error
        };

        serializer.Serialize(writer, config);

        byte[] serialized = ms.ToArray();

        if (modifiers.HasFlag(ConfigModifiers.Compressed))
        {
            serialized = Zip(serialized);
        }

        File.WriteAllBytes(filePath, serialized);
    }

    private ConfigFileAttribute GetConfigAttribute(Type configType)
    {
        if (_cachedAttributes.TryGetValue(configType, out ConfigFileAttribute? attribute))
        {
            return attribute;
        }

        attribute = GetNoCacheConfigAttribute(configType);
        _cachedAttributes.Add(configType, attribute);

        return attribute;
    }

    private static ConfigFileAttribute GetNoCacheConfigAttribute(Type configType)
    {
        return configType.GetCustomAttributes(typeof(ConfigFileAttribute), true)
            .Cast<ConfigFileAttribute>()
            .First();
    }

    /* Compression */

    private static byte[] Zip(byte[] bytes)
    {
        using MemoryStream msi = new(bytes);
        using MemoryStream mso = new();
        using (GZipStream gs = new(mso, CompressionMode.Compress))
        {
            msi.CopyTo(gs);
        }

        return mso.ToArray();
    }

    private static byte[] Unzip(byte[] bytes)
    {
        using MemoryStream msi = new(bytes);
        using MemoryStream mso = new();
        using (GZipStream gs = new(msi, CompressionMode.Decompress))
        {
            gs.CopyTo(mso);
        }

        return mso.ToArray();
    }

    private string GetCompletePath(string configName)
    {
        return Path.Combine(ConfigurationDirectory, configName);
    }

    /// <summary>
    /// Removes all config files under the config directory.
    /// Should only be called when the user wants to uninstall APKognito.
    /// </summary>
    /// <param name="security"></param>
    public void W_RemoveAllConfigurationFiles(bool security = false)
    {
        // Cheap way to ensure it's not called by accident
        if (!security)
        {
            return;
        }

        try
        {
            Directory.Delete(ConfigurationDirectory, true);
        }
        catch (Exception ex)
        {
            FileLogger.LogException("Failed to delete configuration directory", ex);
        }
    }

    public sealed class UnknownConfigTypeException(ConfigType configType) : Exception($"No implementation found for '{configType}'")
    {
    }
}
