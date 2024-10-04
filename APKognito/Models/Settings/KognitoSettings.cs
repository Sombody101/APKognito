using APKognito.ViewModels.Pages;
using APKognito.Views.Pages;
using Newtonsoft.Json;
using System.IO;
using System.Runtime.InteropServices;

namespace APKognito.Models.Settings;

internal static class KognitoSettings
{
    private const string configsPath = "./config";
    private const string settingsPath = $"{configsPath}/settings.json";

    private static HomePage? _homePage = HomePage.Instance;
    private static KognitoConfig? _globalInstance;

    public static List<string>? PrePageErrorLogs;

    static KognitoSettings()
    {
        try
        {
            // Ensure the directory exists
            _ = Directory.CreateDirectory(configsPath);
        }
        catch (Exception ex)
        {
            TryLogError($"Failed to create configuration directory: {ex.Message}");
        }
    }

    public static KognitoConfig GetSettings()
    {
        return _globalInstance ??= DeserializeFromFile(settingsPath, () =>
        {
            TryLogError($"No config found! Creating a new one with default values.");

            KognitoConfig newConfig = new();
            SerializeToFile(newConfig, settingsPath);
            return newConfig;
        }, (ex) =>
        {
            TryLogError($"Error loading settings: {ex.Message}");
            return new();
        });
    }

    public static void SaveSettings()
    {
        SerializeToFile(_globalInstance, settingsPath, (ex) => TryLogError($"Error saving settings: {ex.Message}"));
    }

    public static T DeserializeFromFile<T>(string filePath, [Optional] Func<T> notFoundCallback, [Optional] Func<Exception, T> errorCallback) where T : class, new()
    {
        if (!File.Exists(filePath))
        {
            return notFoundCallback?.Invoke() ?? new();
        }

        try
        {
            using StreamReader reader = new(filePath);
            using JsonTextReader jsonReader = new(reader);
            JsonSerializer serializer = new()
            {
                NullValueHandling = NullValueHandling.Ignore,
            };

            return serializer.Deserialize<T>(jsonReader)!;
        }
        catch (Exception ex)
        {
            return errorCallback?.Invoke(ex) ?? new();
        }
    }

    public static void SerializeToFile<T>(this T data, string filePath, [Optional] Action<Exception> errorCallback)
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
            errorCallback?.Invoke(ex);
        }
    }

    private static void TryLogError(string log)
    {
        if (_homePage is null)
        {
            PrePageErrorLogs ??= [];
            PrePageErrorLogs.Add(log);

            // Check if the home page is still null, get the reference if not
            if (HomePage.Instance is not null)
                _homePage = HomePage.Instance;

            return;
        }

        _homePage.ViewModel.LogError(log);
    }
}