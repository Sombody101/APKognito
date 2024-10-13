using Newtonsoft.Json;

namespace APKognito.Configurations.ConfigModels;

[ConfigFile("update.json", ConfigType.Json, ConfigModifier.JsonIndented | ConfigModifier.JsonIgnoreMissing)]
internal class UpdateConfig : IKognitoConfig
{
    /// <summary>
    /// If <see langword="true"/>, APKognito will automatically check and download updates. 
    /// They are implemented when restarting.
    /// </summary>
    [JsonProperty("check_for_updates")]
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>
    /// The number of minutes to wait between checks.
    /// </summary>
    [JsonProperty("update_minute_interval")]
    public int CheckDelay { get; set; } = 60; // Once every hour
}
