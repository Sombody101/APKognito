﻿using APKognito.Utilities;
using MemoryPack;
using Newtonsoft.Json;
using System.IO;
using System.Runtime.InteropServices;

namespace APKognito.Configurations;

public static class ConfigurationFactory
{
    // Keeps the configs "secure" (classes will still have references to each instance, but this ensures there can only be one instance in use at a time)
    private static readonly Dictionary<Type, IKognitoConfig> _cachedConfigs = [];
    private static readonly Dictionary<Type, ConfigFileAttribute> _cachedAttributes = [];

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
    public static void SaveConfig(IKognitoConfig config, [Optional] ConfigFileAttribute? configAttribute, [Optional] bool forceAppdataSave)
    {
        configAttribute ??= GetConfigAttribute(config.GetType())!;

        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configAttribute.FileName);

        if (!configAttribute.LoadedFromCurrentDirectory || forceAppdataSave)
        {
            FileLogger.Log($"'{filePath}' for {config.GetType().Name}");
            filePath = configAttribute.GetCompletePath();
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

    /// <summary>
    /// Transfers all valid configuration files found in the apps startup directory to the respective place in %APPDATA%.
    /// </summary>
    public static void TransferAppStartConfigurations()
    {
        foreach (var config in _cachedConfigs)
        {
            var attr = GetConfigAttribute(config.Key);
            if (attr.LoadedFromCurrentDirectory)
            {
                SaveConfig(config.Value, attr, true);

                // Reset it to save to %APPDATA% on close
                attr.RevertSaveToCurrent();

                try
                {
                    // Remove the file once transfered
                    File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, attr.FileName));
                }
                catch (Exception ex)
                {
                    FileLogger.LogError($"Failed to delete '{attr.FileName}' after transferring it to %APPDATA%: {ex.Message}");
                }
            }
        }
    }

    private static T LoadConfig<T>(ConfigFileAttribute configAttribute) where T : IKognitoConfig, new()
    {
        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configAttribute.FileName);

        // Check if the file was moved into the same directory as the app
        if (!File.Exists(filePath))
        {
            filePath = configAttribute.GetCompletePath();
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

    private static T? Load_Json<T>(string filePath, ConfigModifiers modifier) where T : IKognitoConfig, new()
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
            MissingMemberHandling = modifier.HasFlag(ConfigModifiers.JsonIgnoreMissing)
                ? MissingMemberHandling.Ignore
                : MissingMemberHandling.Error,
        };

        return serializer.Deserialize<T>(jsonReader)!;
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

    private static ConfigFileAttribute GetConfigAttribute(Type configType)
    {
        if (_cachedAttributes.TryGetValue(configType, out var attribute))
        {
            return attribute;
        }

        attribute = configType.GetCustomAttributes(typeof(ConfigFileAttribute), true)
            .Cast<ConfigFileAttribute>()
            .First();

        _cachedAttributes.Add(configType, attribute);

        return attribute;
    }
}