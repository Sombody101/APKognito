using APKognito.ApkLib.Configuration;
using Newtonsoft.Json;

namespace APKognito.Configurations.ConfigModels;

[ConfigFile("adv-rename.json", ConfigType.Json, ConfigModifiers.JsonIndented | ConfigModifiers.JsonIgnoreMissing)]
public class AdvancedApkRenameSettings : IKognitoConfig
{
    public const string DEFAULT_RENAME_REGEX = "(?<=[./_])({value})(?=[./_])";
    public const string DEFAULT_JAVA_ADDED_FLAGS = "--enable-native-access=ALL-UNNAMED ";

    /// <summary>
    /// The regex to be used on a package name to rename it. Use <see cref="BuildRegex(string, int)"/> to get the compiled form.
    /// </summary>
    [JsonProperty("package_replace_regex")]
    public string PackageReplaceRegexString { get; set; } = DEFAULT_RENAME_REGEX;

    [JsonProperty("java_flags")]
    public string JavaFlags { get; set; } = DEFAULT_JAVA_ADDED_FLAGS;

    /// <summary>
    /// Renames the literal library file (e.g., "libappname.so" -> "libapkognito.so")
    /// </summary>
    [JsonProperty("rename_libs")]
    public bool RenameLibs { get; set; } = false;

    /// <summary>
    /// Specifies for library files (.SO) string table sections to be renamed.
    /// </summary>
    [JsonProperty("rename_libs_internal")]
    public bool RenameLibsInternal { get; set; } = false;

    /// <summary>
    /// Reads through all the entries of an OBB and renames assets where needed.
    /// This only applies to archives, asset bundles under the name of an OBB.
    /// </summary>
    [JsonProperty("rename_obbs_internal")]
    public bool RenameObbsInternal { get; set; } = true;

    /// <summary>
    /// Extra files within OBB archives.
    /// </summary>
    [JsonProperty("rename_obbs_internal_extras")]
    public List<string> RenameObbsInternalExtras { get; set; } = [];

    /// <summary>
    /// Extra files to force rename. These paths must be relative to the unpacked APK root. (i.g., "/AndroidManifest.xml" rather than "C:\...\AndroidManifest.xml")
    /// </summary>
    [JsonProperty("extra_internal_package_paths")]
    public List<ExtraPackageFile> ExtraInternalPackagePaths { get; set; } = [];

    [JsonProperty("auto_package_config_enabled")]
    public bool AutoPackageEnabled { get; set; } = false;

    /// <summary>
    /// This might be unsafe, but will help with some apps.
    /// </summary>
    [JsonProperty("auto_package_config")]
    public string? AutoPackageConfig { get; set; }

    /*
     * Buffers
     */

    [JsonProperty("smali_cutoff_limit")]
    public int SmaliCutoffLimit { get; set; } = 1024 * 1024;

    [JsonProperty("smali_buffer_size")]
    public int SmaliBufferSize { get; set; } = 1024 * 64;

    [JsonProperty("scan_smali_before_rename")]
    public bool ScanFileBeforeRename { get; set; } = false;
}
