using Newtonsoft.Json;

namespace APKognito.Models.Settings;

public class KognitoConfig
{
    [JsonProperty("last_apk_input")]
    public string? ApkSourcePath { get; set; }

    [JsonProperty("apk_output")]
    public string? ApkOutputDirectory { get; set; }

    [JsonProperty("apk_replacement_name")]
    public string? ApkNameReplacement { get; set; }

    [JsonProperty("check_for_updates")]
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>
    /// Specifies where to open the FileDialog to select an APK
    /// </summary>
    [JsonProperty("dialog_directory")]
    public string? LastDialogDirectory { get; set; } = AppDomain.CurrentDomain.BaseDirectory;
}
