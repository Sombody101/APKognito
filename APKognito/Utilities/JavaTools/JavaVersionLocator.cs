using System.IO;
using System.Text;
using APKognito.AdbTools;
using APKognito.Utilities.MVVM;
using Microsoft.Win32;

namespace APKognito.Utilities.JavaTools;

[Obsolete("Use JavaVersionCollector instead.")]
internal class JavaVersionLocator
{
    private static JavaVersionInformation? CachedJavaInformation { get; set; }

    private readonly IViewLogger? _logger = null;

    public JavaVersionLocator()
        : this(null)
    {
    }

    public JavaVersionLocator(IViewLogger? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the Java path (JDK or JRE) that is Java Language 8+, and returns <see langword="true"/>. Otherwise, retuning <see langword="false"/> and <paramref name="javaInfo"/>
    /// as <see langword="null"/>
    /// </summary>
    /// <param name="javaInfo"></param>
    /// <param name="logger"></param>
    /// <param name="forceSearch"></param>
    /// <returns></returns>
    public bool GetJavaPath([NotNullWhen(true)] out JavaVersionInformation? javaInfo, bool forceSearch = false)
    {
        if (!forceSearch && CachedJavaInformation is { UpToDate: true } && File.Exists(CachedJavaInformation.JavaPath))
        {
            _logger?.Log($"Using cached Java version '{CachedJavaInformation.RawVersion}'.");

            javaInfo = CachedJavaInformation;
            return true;
        }

        // Get Java path
        bool pathFound = VerifyJavaInstallation(out javaInfo);
        if (pathFound)
        {
            FileLogger.Log($"Using found Java version '{javaInfo!.RawVersion}'");
        }
        else
        {
            FileLogger.LogWarning("Failed to find any valid Java installations.");
        }

        CachedJavaInformation = javaInfo;
        return pathFound;
    }

    private bool VerifyJavaInstallation([NotNullWhen(true)] out JavaVersionInformation? javaInfo)
    {
        javaInfo = null;

        // Check for JDK via registry (there's likely not a Java install if these don't exist)
        try
        {
            FileLogger.Log("Checking Registry for JDK/JRE installation.");
            if (CheckRegistryKeys(out javaInfo))
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
        }

        // Checks C:\Program Files\Java\latest
        try
        {
            FileLogger.Log("Checking for Java LTS directory.");
            if (CheckLtsDirectory(out javaInfo))
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
        }

        // Checks the JAVA_HOME environment variable (requires jimmy-rigged parsing, not super reliable)
        try
        {
            FileLogger.Log("Checking JAVA_HOME environment variable.");
            if (CheckJavaHome(out javaInfo))
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
        }

        // Nothing found, inform user and yeet false
        _logger?.LogError($"Failed to find a valid JDK/JRE installation!\n" +
            "You can install JDK 24 by navigating to the ADB Configuration page and running the installation quick command.\n" +
            "Alternatively, you can run the command `:install-java` in the Console page, or manually install a preferred version.\n" +
            $"\tJDK 24: {AdbManager.JDK_24_INSTALL_EXE_LINK}");

        return false;
    }

    private bool CheckRegistryKeys([NotNullWhen(true)] out JavaVersionInformation? javaInfo)
    {
        if (GetKey(Registry.LocalMachine.OpenSubKey("SOFTWARE\\JavaSoft\\JDK"), out JavaVersionInformation? javaKeyInfo, "JDK")
            || GetKey(Registry.LocalMachine.OpenSubKey("SOFTWARE\\JavaSoft\\Java Runtime Environment"), out javaKeyInfo, "JRE"))
        {
            javaInfo = javaKeyInfo;
            return true;
        }

        javaInfo = null;
        return false;
    }

    private static bool CheckLtsDirectory([NotNullWhen(true)] out JavaVersionInformation? javaInfo)
    {
        const string JAVA_LTS_DIRECTORY = @"C:\Program Files\Java\latest";

        javaInfo = null;

        if (!Directory.Exists(JAVA_LTS_DIRECTORY))
        {
            FileLogger.Log($"No Java LTS directory found: {JAVA_LTS_DIRECTORY}");
            return false;
        }

        try
        {
            // Check for 'latest' directory, use the shortcut to the latest JRE version.
            string[] dirs = Directory.GetDirectories(JAVA_LTS_DIRECTORY);
            if (dirs.Length > 0)
            {
                DirectoryInfo latest = new(dirs[0]);

                if (VerifyRawJavaVersion(latest.Name, out Version? jdkVersion))
                {
                    javaInfo = new(latest.FullName, jdkVersion, latest.Name);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
        }

        return false;
    }

    private static bool CheckJavaHome([NotNullWhen(true)] out JavaVersionInformation? javaInfo)
    {
        javaInfo = null;

        // Check with the environment variable first
        string? javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");

        FileLogger.Log($"Checking JAVA_HOME: {javaHome}");
        string dirVersionName = Path.GetFileName(javaHome) ?? string.Empty;

        if (dirVersionName.StartsWith("jdk"))
        {
            // JDK
            // C:\Program Files\Java\jdk-23
            // jdk-23 -> 23
            dirVersionName = dirVersionName[4..];
        }
        else if (dirVersionName.StartsWith("jre"))
        {
            const int prefixLen = 3;

            // JRE
            // C:\Program Files\Java\jre1.8.0_431
            // jre1.8.0_431 -> 1.8.0

            int endIndex = dirVersionName.LastIndexOf('_');
            if (endIndex is -1)
            {
                endIndex = dirVersionName.Length - prefixLen;
            }

            dirVersionName = dirVersionName[prefixLen..endIndex];
        }

        if (string.IsNullOrWhiteSpace(javaHome)
            || !Directory.Exists(javaHome)
            || !VerifyRawJavaVersion(dirVersionName, out Version? jdkVersion))
        {
            return false;
        }

        string javaPath = Path.Combine(javaHome, "bin\\java.exe");
        if (File.Exists(javaPath))
        {
            javaInfo = new(javaPath, jdkVersion, dirVersionName);
            return true;
        }

        // Java installation is messed up
        LogInvalidInstallation(javaHome);

        return false;
    }

    private bool GetKey(RegistryKey? javaRegKey, [NotNullWhen(true)] out JavaVersionInformation? javainfo, string javaType)
    {
        javainfo = null;

        if (javaRegKey is null)
        {
            FileLogger.LogWarning($"Registry key for {javaType} doesn't exist.");
            return false;
        }

        FileLogger.Log($"Checking Registry key '{javaRegKey.Name}'.");

        if (javaRegKey.GetValue("CurrentVersion") is not string rawJavaVersion)
        {
            _logger?.LogWarning($"A {javaType} installation key was found, but there was no Java version associated with it. Did a Java installation or uninstallation not complete correctly?");
            return false;
        }

        if (!VerifyRawJavaVersion(rawJavaVersion, out Version? jdkVersion))
        {
            _logger?.LogWarning($"{javaType} installation found with the version {rawJavaVersion}, but it's not Java 8+.");
            return false;
        }

        string keyPath = (string)javaRegKey.OpenSubKey(rawJavaVersion)!.GetValue("JavaHome")!;
        string javaPath = Path.Combine(keyPath, "bin\\java.exe");

        // This is a VERY rare case
        if (!File.Exists(javaPath))
        {
            _logger?.LogError($"Java version {rawJavaVersion} found, but the Java directory it points to does not exist: {javaPath}");
            return false;
        }

        _logger?.Log($"Using Java version {rawJavaVersion} at {javaPath}");
        javainfo = new(javaPath, jdkVersion, rawJavaVersion);
        return true;
    }

    private static bool VerifyRawJavaVersion(string versionStr, [NotNullWhen(true)] out Version? version)
    {
        return Version.TryParse(versionStr, out version) && JavaVersionInformation.VersionUpToDate(version, versionStr);
    }

    private static void LogInvalidInstallation(string javaPath)
    {
        StringBuilder logBuffer = new();

        _ = logBuffer.Append("JAVA_HOME is set to '")
            .Append(javaPath).Append("', but does not have java.exe. bin\\: does ");

        bool binExists = Directory.Exists($"{javaPath}\\bin");
        if (!binExists)
        {
            _ = logBuffer.Append("not ");
        }

        _ = logBuffer.Append("exist.");

        if (binExists)
        {
            _ = logBuffer.AppendLine(" Files found in bin\\:");
            foreach (string file in Directory.GetFiles(javaPath))
            {
                _ = logBuffer.Append('\t').AppendLine(Path.GetFileName(file));
            }
        }

        FileLogger.LogError(logBuffer.ToString());
    }
}
