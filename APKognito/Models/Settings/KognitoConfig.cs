using Newtonsoft.Json;

namespace APKognito.Models.Settings;

public class KognitoConfig
{
    [JsonProperty("last_apk_input")]
    public string? ApkSourcePath { get; set; }

    [JsonProperty("apk_output")]
    public string? ApkOutputDirectory { get; set; }

    public string ApkNameReplacement { get; set; } = "apkognito";

    public List<string> ApkHistory { get; set; } = [];

    /// <summary>
    /// Specifies where to open the FileDialog to select an APK
    /// </summary>
    public string? LastDialogDirectory { get; set; } = AppDomain.CurrentDomain.BaseDirectory;
}
