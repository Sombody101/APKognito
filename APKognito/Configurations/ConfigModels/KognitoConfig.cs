using APKognito.Configurations;
using Newtonsoft.Json;
using System.IO;

namespace APKognito.Models.Settings;

public class KognitoConfig : IKognitoConfiguration
{
    public string FileName => "settings.json";

    public ConfigType ConfigType => ConfigType.JsonIndented;

    /// <summary>
    /// The directory to push the new files to.
    /// </summary>
    [JsonProperty("apk_output")]
    public string ApkOutputDirectory { get; set; } = Path.Combine(App.AppData.FullName, "output");

    /// <summary>
    /// The name to replace the company name with in an APK (com.&lt;your_company&gt;.app -> com.apkognito.app)
    /// </summary>
    [JsonProperty("apk_replacement_name")]
    public string? ApkNameReplacement { get; set; }

    /// <summary>
    /// WIP
    /// </summary>
    [JsonProperty("check_for_updates")]
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>
    /// Rather than moving the files, preserves the old game.
    /// </summary>
    [JsonProperty("copy_when_renaming")]
    public bool CopyFilesWhenRenaming { get; set; } = true;

    /// <summary>
    /// Holds old APK paths to load (at least so there's content to present).
    /// </summary>
    [JsonProperty("last_apk_input--TEMP")]
    public string? ApkSourcePath { get; set; }

    /// <summary>
    /// Specifies where to open the FileDialog to select an APK.
    /// </summary>
    [JsonProperty("dialog_directory--TEMP")]
    public string? LastDialogDirectory { get; set; } = AppDomain.CurrentDomain.BaseDirectory;
}
