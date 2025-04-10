using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace APKognito.Configurations.ConfigModels;

[ConfigFile("adv-rename.json", ConfigType.Json, ConfigModifiers.JsonIndented | ConfigModifiers.JsonIgnoreMissing)]
public class AdvancedApkRenameSettings : IKognitoConfig
{
    public const string DEFAULT_RENAME_REGEX = "(?<=[./_])({value})(?=[./_])";

    [JsonProperty("package_replace_regex")]
    public string PackageReplaceRegexString { get; set; } = DEFAULT_RENAME_REGEX;

    [JsonProperty("rename_libs")]
    public bool RenameLibs { get; set; } = false;

    [JsonProperty("rename_libs_internal")]
    public bool RenameLibsInternal { get; set; } = false;

    [JsonProperty("rename_obbs_internal")]
    public bool RenameObbsInternal { get; set; } = true;

    [JsonProperty("rename_obbs_internal_extras")]
    public List<string> RenameObbsInternalExtras { get; set; } = [];

    public Regex BuildRegex(string value, int regexTimeoutMs = 60_000)
    {
        string pattern = PackageReplaceRegexString.Replace("{value}", value);

        return new Regex(pattern,
            RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(regexTimeoutMs)
        );
    }
}