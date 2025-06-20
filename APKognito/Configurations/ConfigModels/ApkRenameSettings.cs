using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using APKognito.ApkLib.Configuration;

namespace APKognito.Configurations.ConfigModels;

public sealed class ApkRenameSettings
{
    /// <summary>
    /// The source file path of the APK to be renamed.
    /// </summary>
    public string SourceApkPath { get; init; } = string.Empty;

    /// <summary>
    /// If null, then ApkLib will use <see cref="OutputBaseDirectory"/> decide the output directory name. If both have a value, ApkLib will default
    /// to this.
    /// 
    /// This will be set to the directory created by ApkLib with <see cref="OutputBaseDirectory"/> if <see langword="null"/>.
    /// </summary>
    public string? OutputDirectory { get; set; }

    /// <summary>
    /// The base directory to output the renamed APK into. A child directory will be created and have the contents placed into that.
    /// </summary>
    public string? OutputBaseDirectory { get; init; }

    public string JavaPath { get; init; } = string.Empty;

    public string TempDirectory { get; init; } = string.Empty;

    public string ApkReplacementName { get; init; } = string.Empty;

    public bool CopyFilesWhenRenaming { get; init; }

    public bool ClearTempFilesOnRename { get; init; }

    public string ApktoolJarPath { get; init; } = string.Empty;

    public string ApktoolBatPath { get; init; } = string.Empty;

    public string ApksignerJarPath { get; init; } = string.Empty;

    /// <summary>
    /// The regex to be used on a package name to rename it. Use <see cref="BuildRegex(string, int)"/> to get the compiled form.
    /// </summary>
    public required string PackageReplaceRegexString { get; init; }

    /// <summary>
    /// Renames the literal library file (e.g., "libappname.so" -> "libapkognito.so")
    /// </summary>
    public bool RenameLibs { get; init; } = false;

    /// <summary>
    /// Specifies for library files (.SO) string table sections to be renamed.
    /// </summary>
    public bool RenameLibsInternal { get; init; } = false;

    /// <summary>
    /// Reads through all the entries of an OBB and renames assets where needed.
    /// This only applies to archives, asset bundles under the name of an OBB.
    /// </summary>
    public bool RenameObbsInternal { get; init; } = true;

    /// <summary>
    /// Extra files within OBB archives.
    /// </summary>
    public List<string> RenameObbsInternalExtras { get; init; } = [];

    /// <summary>
    /// Extra files to force rename. These paths must be absolute from the APK root. (i.g., "/AndroidManifest.xml" rather than "C:\...\AndroidManifest.xml")
    /// </summary>
    public List<ExtraPackageFile> ExtraInternalPackagePaths { get; init; } = [];

    public bool AutoPackageEnabled { get; init; } = false;

    /// <summary>
    /// This might be unsafe, but will help with some apps.
    /// </summary>
    public string? AutoPackageConfig { get; init; }

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
