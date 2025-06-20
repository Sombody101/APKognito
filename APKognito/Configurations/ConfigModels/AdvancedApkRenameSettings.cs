using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using APKognito.ApkLib.Configuration;
using Newtonsoft.Json;

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
    /// The number of threads the thread pool is allowed to use. (set to 1 to disable multi threading)
    /// </summary>
    [JsonProperty("threads")]
    public int ThreadCount
    {
        get;
        set
        {
            field = value;
            ThreadPool.SetMaxThreads(value, value);
        }
    } = Environment.ProcessorCount;

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

    [JsonProperty("auto_package_config_enabled")]
    public bool AutoPackageEnabled { get; set; } = false;

    /// <summary>
    /// This might be unsafe, but will help with some apps.
    /// </summary>
    [JsonProperty("auto_package_config")]
    public string? AutoPackageConfig { get; set; }

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

    /// <summary>
    /// Combines a user supplied package path with the real-world drive path. An <see cref="InvalidExtraPathException"/>
    /// will be thrown if the user supplied path escapes the package directory (i.e., "C:\Path\To\Package + /../../../../random-file.json")
    /// </summary>
    /// <param name="drivePath"></param>
    /// <param name="userPath"></param>
    /// <param name="noRoot"></param>
    /// <returns></returns>
    /// <exception cref="InvalidExtraPathException"></exception>
    public static string SafeCombine(string drivePath, string userPath, bool noRoot = true)
    {
        if (userPath.Any(char.IsControl))
        {
            throw new UnsafeExtraPathException($"The given path '{CleanStringBytes(userPath)}' contains control characters.");
        }

        drivePath = drivePath.TrimEnd('\\');
        string packagePath = userPath.Replace('/', '\\').TrimStart('\\');

        string normalizedBasePath = Path.GetFullPath(drivePath);
        string combinedPath = Path.GetFullPath(Path.Combine(normalizedBasePath, packagePath));

        if (noRoot && combinedPath.Equals(normalizedBasePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnsafeExtraPathException($"The given path '{userPath}' attempts to modify the project root directory.");
        }

        if (combinedPath.StartsWith(normalizedBasePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return combinedPath;
        }

        throw new UnsafeExtraPathException($"The given path '{userPath}' escapes the package directory.");
    }

    private static string CleanStringBytes(string offending)
    {
        StringBuilder output = new();

        foreach (char c in offending)
        {
            if (!char.IsControl(c))
            {
                output.Append(c);
                continue;
            }

            output.Append($"(0x{(byte)c:x2})");
        }

        return output.ToString();
    }

    public class InvalidExtraPathException(string message) : Exception(message)
    {
    }

    public class UnsafeExtraPathException(string message) : Exception(message)
    {
    }
}
