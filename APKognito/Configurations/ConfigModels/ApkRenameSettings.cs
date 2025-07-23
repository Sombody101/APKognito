using APKognito.ApkLib.Configuration;

namespace APKognito.Configurations.ConfigModels;

[Obsolete]
public sealed record ApkRenameSettings
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

    public class InvalidExtraPathException(string message) : Exception(message)
    {
    }

    public class UnsafeExtraPathException(string message) : Exception(message)
    {
    }
}
