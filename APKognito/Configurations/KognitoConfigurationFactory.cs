using MemoryPack;
using Newtonsoft.Json;
using System.IO;

namespace APKognito.Configurations;

public class KognitoConfigurationFactory
{
    private readonly Dictionary<Type, IKognitoConfiguration> _cachedConfigs = [];

    public T GetConfig<T>() where T : IKognitoConfiguration, new()
    {
        Type configType = typeof(T);

        if (_cachedConfigs.ContainsKey(configType))
        {
            return (T)_cachedConfigs[configType];
        }

        T instance = new();
        var fileName = instance.FileName;

        var config = LoadConfig<T>(fileName, instance, instance.ConfigType);
        _cachedConfigs[configType] = config;
        return config;
    }

    public void SaveConfig<T>(T config) where T : IKognitoConfiguration
    {
        var fileName = config.FileName;

        switch (config.ConfigType)
        {
            case ConfigType.Json:
                Save_Json(config, fileName, Formatting.None);
                break;

            case ConfigType.JsonIndented:
                Save_Json(config, fileName, Formatting.Indented);
                break;

            case ConfigType.MemoryPacked:
                Save_MemoryPack<T>(config, fileName);
                break;
        }

        _cachedConfigs[typeof(T)] = config;
    }

    public void SaveAllConfigs()
    {
        foreach (var config in _cachedConfigs.Select(cfg => cfg.Value))
        {
            SaveConfig(config);
        }
    }

    private T LoadConfig<T>(string fileName, T instance, ConfigType configType) where T : IKognitoConfiguration, new()
    {
        switch (configType)
        {
            case ConfigType.Json:
            case ConfigType.JsonIndented:
                return Load_Json<T>(fileName) ?? instance;

            case ConfigType.MemoryPacked:
                return Load_MemoryPack<T>(fileName) ?? instance;
        }

        return new T();
    }

    private T? Load_MemoryPack<T>(string fileName) where T : IKognitoConfiguration, new()
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

    private void Save_MemoryPack<T>(T config, string fileName) where T : IKognitoConfiguration
    {
        string filePath = GetAppdataFile(fileName);

        byte[] packed = MemoryPackSerializer.Serialize(config);

        File.WriteAllBytes(filePath, packed);
    }

    private T? Load_Json<T>(string fileName) where T : IKognitoConfiguration, new()
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
        };

        return serializer.Deserialize<T>(jsonReader)!;
    }

    private void Save_Json<T>(T config, string fileName, Formatting formatting) where T : IKognitoConfiguration
    {
        using StreamWriter writer = new(GetAppdataFile(fileName));
        using JsonTextWriter jsonWriter = new(writer);
        JsonSerializer serializer = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = formatting
        };

        serializer.Serialize(jsonWriter, config);
    }

    private static string GetAppdataFile(string filename)
    {
        string configs = Path.Combine(App.AppData!.FullName, "config");
        _ = Directory.CreateDirectory(configs);
        return Path.Combine(configs, filename);
    }
}
