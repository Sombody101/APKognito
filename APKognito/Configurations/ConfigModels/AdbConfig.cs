using Newtonsoft.Json;
using System.IO;

namespace APKognito.Configurations.ConfigModels;

[ConfigFile("adb-config.json", ConfigType.Json, ConfigModifiers.JsonIndented | ConfigModifiers.JsonIgnoreMissing)]
internal class AdbConfig : IKognitoConfig
{
    /// <summary>
    /// Defaults to the platform tools installed with Android Studio
    /// </summary>
    [JsonProperty("adb_path")]
    public string PlatformToolsPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android\\Sdk\\platform-tools");


    /*
     * Non-permeant fields
     */

    [JsonIgnore]
    public string? CurrentDeviceId { get; set; } = null;
}
