using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    public ConfigModifier ConfigModifier { get; }

    public ConfigFileAttribute(
        string fileName, 
        ConfigType configType = ConfigType.Json, 
        ConfigModifier configModifier = ConfigModifier.None)
    {
        FileName = fileName;
        ConfigType = configType;
        ConfigModifier = configModifier;
    }
}

public interface IKognitoConfig
{
}
