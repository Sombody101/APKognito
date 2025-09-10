using System.IO;
using APKognito.ApkLib.Configuration;
using APKognito.Utilities.JavaTools;
using Newtonsoft.Json;

namespace APKognito.Configurations.ConfigModels;

[ConfigFile("rename-settings.json",
    ConfigType.Json,
    ConfigModifiers.JsonIndented | ConfigModifiers.JsonIgnoreMissing)]
public sealed partial class UserRenameConfiguration : ObservableObject, IKognitoConfig
{
    /// <summary>
    /// The directory to push the new files to.
    /// </summary>
    [JsonProperty("apk_output")]
    [ObservableProperty]
    public partial string ApkOutputDirectory { get; set; } = Path.Combine(App.AppDataDirectory!.FullName, "output");

    [JsonProperty("apk_pull_output")]
    [ObservableProperty]
    public partial string ApkPullDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

    /// <summary>
    /// The name to replace the company name with in an APK (com.&lt;your_company&gt;.app -> com.apkognito.app)
    /// </summary>
    [JsonProperty("apk_replacement_name")]
    [ObservableProperty]
    public partial string ApkNameReplacement { get; set; } = "apkognito";

    /// <summary>
    /// Rather than moving the files, preserves the old game.
    /// </summary>
    [JsonProperty("copy_when_renaming")]
    [ObservableProperty]
    public partial bool CopyFilesWhenRenaming { get; set; } = true;

    [JsonProperty("clear_temp_on_rename")]
    [ObservableProperty]
    public partial bool ClearTempFilesOnRename { get; set; } = true;

    [JsonProperty("push_after_rename")]
    [ObservableProperty]
    public partial bool PushAfterRename { get; set; } = false;

    [JsonProperty("java_version")]
    public string? SelectedRawJavaVersion { get; set; }

    [JsonIgnore]
    public PackageToolingPaths BaseToolingPaths { get; set; } = new();

    public (PackageToolingPaths, JavaVersionInformation) GetToolingPaths(JavaVersionCollector collector)
    {
        JavaVersionInformation wantedVersion = collector.GetVersion(SelectedRawJavaVersion);

        return (new()
        {
            JavaExecutablePath = wantedVersion.JavaPath,
            ApkSignerJarPath = BaseToolingPaths.ApkSignerJarPath,
            ApkToolBatPath = BaseToolingPaths.ApkToolBatPath,
            ApkToolJarPath = BaseToolingPaths.ApkToolJarPath
        }, wantedVersion);
    }
}
