using System.IO;

namespace APKognito.Configurations;

/// <summary>
/// The name of the file for a config. (e.g. <see langword="specific-config.json"/>).
/// All configs will be stored in <see langword="%APPDATA%"/> under <see langword="APKognito"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ConfigFileAttribute : Attribute
{
    public string FileName { get; }

    public ConfigType ConfigType { get; }

    public ConfigModifiers ConfigModifier { get; }

    public bool LoadedFromCurrentDirectory { get; private set; }

    public ConfigFileAttribute(
        string fileName,
        ConfigType configType = ConfigType.Json,
        ConfigModifiers configModifier = ConfigModifiers.None)
    {
        FileName = fileName;
        ConfigType = configType;
        ConfigModifier = configModifier;
    }

    public string GetCompletePath()
    {
        string configs = Path.Combine(App.AppData!.FullName, "config");
        _ = Directory.CreateDirectory(configs);
        return Path.Combine(configs, FileName);
    }

    public void LoadedFromCurrent()
    {
        LoadedFromCurrentDirectory = true;
    }
}

public interface IKognitoConfig
{
}