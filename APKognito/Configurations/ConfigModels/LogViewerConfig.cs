using Newtonsoft.Json;

namespace APKognito.Configurations.ConfigModels;

[ConfigFile("logview.json", ConfigType.Json, ConfigModifiers.JsonIndented | ConfigModifiers.JsonIgnoreMissing)]
internal class LogViewerConfig : IKognitoConfig
{
    [JsonProperty("recents")]
    public List<string> RecentPacks { get; set; } = [];
}
