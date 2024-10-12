using APKognito.Configurations;
using Newtonsoft.Json;
using System.IO;

namespace APKognito.Models.Settings;

[ConfigFile(
    "settings.json",
    ConfigType.Json,
    ConfigModifier.JsonIndented | ConfigModifier.JsonIgnoreMissing)]
public class KognitoConfig : IKognitoConfig
{
    /// <summary>
    /// The directory to push the new files to.
    /// </summary>
    [JsonProperty("apk_output")]
    public string ApkOutputDirectory { get; set; } = Path.Combine(App.AppData!.FullName, "output");

    /// <summary>
    /// The name to replace the company name with in an APK (com.&lt;your_company&gt;.app -> com.apkognito.app)
    /// </summary>
    [JsonProperty("apk_replacement_name")]
    public string? ApkNameReplacement { get; set; }

    /// <summary>
    /// Rather than moving the files, preserves the old game.
    /// </summary>
    [JsonProperty("copy_when_renaming")]
    public bool CopyFilesWhenRenaming { get; set; } = true;
}