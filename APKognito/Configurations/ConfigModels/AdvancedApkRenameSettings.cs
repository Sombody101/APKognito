using APKognito.Models;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace APKognito.Configurations.ConfigModels;

[ConfigFile("adv-rename.json", ConfigType.Json, ConfigModifiers.JsonIndented | ConfigModifiers.JsonIgnoreMissing)]
public class AdvancedApkRenameSettings : IKognitoConfig
{
    public const string DEFAULT_RENAME_REGEX = "(?<=[./_])({value})(?=[./_])";

    /// <summary>
    /// The regex to be used on a package name to rename it. Use <see cref="BuildRegex(string, int)"/> to get the compiled form.
    /// </summary>
    [JsonProperty("package_replace_regex")]
    public string PackageReplaceRegexString { get; set; } = DEFAULT_RENAME_REGEX;

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
    /// Extra files to force rename. These paths must be absolute from the APK root. (i.g., "/AndroidManifest.xml" rather than "C:\...\AndroidManifest.xml")
    /// </summary>
    [JsonProperty("extra_internal_package_paths")]
    public List<ExtraPackageFile> ExtraInternalPackagePaths { get; set; } = [];

    /// <summary>
    /// Gets the <see cref="Regex"/> <see cref="PackageReplaceRegexString"/>, compiled, with a default 60 second timeout.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="regexTimeoutMs"></param>
    /// <returns></returns>
    public Regex BuildRegex(string value, int regexTimeoutMs = 60_000)
    {
        string pattern = PackageReplaceRegexString.Replace("{value}", value);

        return new Regex(pattern,
            RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(regexTimeoutMs)
        );
    }
}