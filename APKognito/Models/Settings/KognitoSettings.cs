using APKognito.ViewModels.Pages;
using Newtonsoft.Json;
using System.IO;

namespace APKognito.Models.Settings;

internal static class KognitoSettings
{
    private const string configsPath = "./config";
    private const string settingsPath = $"{configsPath}/settings.json";

    private static KognitoConfig? _globalInstance;

    static KognitoSettings()
    {
        try
        {
            // Ensure the directory exists
            _ = Directory.CreateDirectory(configsPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to create configuration directory: {ex.Message}");
        }
    }

    public static KognitoConfig GetSettings()
    {
        return _globalInstance ??= DeserializeFromFile<KognitoConfig>(settingsPath);
    }

    public static void SaveSettings()
    {
        SerializeToFile(_globalInstance, settingsPath);
    }

    private static T DeserializeFromFile<T>(string filePath) where T : class, new()
    {
        if (!File.Exists(filePath))
        {
            HomeViewModel.Instance!.Log($"No config found! Creating a new one with default values.");

            T newConfig = new();
            SerializeToFile(newConfig, filePath);
            return newConfig;
        }

        try
        {
            using StreamReader reader = new(filePath);
            using JsonTextReader jsonReader = new(reader);
            JsonSerializer serializer = new()
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            return serializer.Deserialize<T>(jsonReader)!;
        }
        catch (Exception ex)
        {
            HomeViewModel.Instance!.LogError($"Error loading settings: {ex.Message}");
            return new();
        }
    }

    private static void SerializeToFile<T>(T data, string filePath)
    {
        try
        {
            using StreamWriter writer = new(filePath);
            using JsonTextWriter jsonWriter = new(writer);
            JsonSerializer serializer = new()
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            serializer.Serialize(jsonWriter, data);
        }
        catch (Exception ex)
        {
            HomeViewModel.Instance!.LogError($"Error saving settings: {ex.Message}");
        }
    }
}