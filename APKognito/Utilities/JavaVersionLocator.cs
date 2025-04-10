using APKognito.AdbTools;
using APKognito.Utilities.MVVM;
using Microsoft.Win32;
using System.IO;
using System.Text;

namespace APKognito.Utilities;

internal class JavaVersionLocator
{
    private LoggableObservableObject? logger = null;

    public static string RawJavaVersion { get; set; } = string.Empty;
    public static string? JavaExecutablePath { get; private set; } = null;
    public static bool JavaIsUpToDate { get; private set; } = false;

    /// <summary>
    /// Gets the Java path (JDK or JRE) that is Java Language 8+, and returns <see langword="true"/>. Otherwise, retuning <see langword="false"/> and <paramref name="javaPath"/>
    /// as <see langword="null"/>
    /// </summary>
    /// <param name="javaPath"></param>
    /// <param name="_logger"></param>
    /// <param name="forceSearch"></param>
    /// <returns></returns>
    public bool GetJavaPath(out string? javaPath, LoggableObservableObject? _logger = null, bool forceSearch = false)
    {
        if (_logger is not null)
        {
            logger = _logger;
        }

        if (!forceSearch && JavaIsUpToDate && File.Exists(JavaExecutablePath))
        {
            // Java already found
            _logger?.Log($"Using cached Java version '{RawJavaVersion}'.");

            javaPath = JavaExecutablePath;
            return true;
        }

        // Get Java path
        JavaIsUpToDate = VerifyJavaInstallation(out javaPath);
        JavaExecutablePath = javaPath;

        if (JavaIsUpToDate && !string.IsNullOrWhiteSpace(RawJavaVersion))
        {
            FileLogger.Log($"Using found Java version '{RawJavaVersion}'");
        }
        else
        {
            FileLogger.LogWarning("Failed to find any valid Java installations.");
        }

        return JavaIsUpToDate;
    }

    private bool VerifyJavaInstallation(out string? javaPath)
    {
        javaPath = null;

        // Check for JDK via registry (there's likely not a Java install if these don't exist)
        try
        {
            FileLogger.Log("Checking Registry for JDK/JRE installation.");
            if (GetKey(Registry.LocalMachine.OpenSubKey("SOFTWARE\\JavaSoft\\Java Runtime Environment"), out javaPath, "JRE")
                || GetKey(Registry.LocalMachine.OpenSubKey("SOFTWARE\\JavaSoft\\JDK"), out javaPath, "JDK"))
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
            if (CheckLtsDirectory(out javaPath))
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
            if (CheckJavaHome(out javaPath))
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
        }

        // Nothing found, inform user and yeet false
        logger?.LogError($"Failed to find a valid JDK/JRE installation!\nYou can install the latest JDK version from here: {AdbManager.JDK_23_INSTALL_LINK}\n" +
            "If you know you have a Java installation, set your JAVA_HOME environment variable to the proper path for your Java installation. " +
            "Alternatively, you can run the command `:install-java` in the Console page");

        return false;
    }

    private static bool CheckLtsDirectory(out string? javaPath)
    {
        const string JAVA_LTS_DIRECTORY = @"C:\Program Files\Java\latest";

        javaPath = null;

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

                if (VerifyRawJavaVersion(latest.Name))
                {
                    javaPath = latest.FullName;
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

    private static bool CheckJavaHome(out string? javaPath)
    {
        javaPath = null;

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
            || !VerifyRawJavaVersion(dirVersionName))
        {
            return false;
        }

        javaPath = Path.Combine(javaHome, "bin\\java.exe");
        if (File.Exists(javaPath))
        {
            return true;
        }

        // Java installation is messed up
        LogInvalidInstallation(javaHome);

        return false;
    }

    private bool GetKey(RegistryKey? javaRegKey, out string? javaPath, string javaType)
    {
        javaPath = null;

        if (javaRegKey is null)
        {
            FileLogger.LogWarning($"Registry key for {javaType} doesn't exist.");
            return false;
        }

        FileLogger.Log($"Checking Registry key '{javaRegKey.Name}'.");

        if (javaRegKey.GetValue("CurrentVersion") is not string _rawJavaVersion)
        {
            logger?.LogWarning($"A {javaType} installation key was found, but there was no Java version associated with it. Did a Java installation or uninstallation not complete correctly?");
            return false;
        }

        RawJavaVersion = _rawJavaVersion;

        if (!VerifyRawJavaVersion(_rawJavaVersion))
        {
            logger?.LogWarning($"{javaType} installation found with the version {_rawJavaVersion}, but it's not Java 8+.");
            return false;
        }

        string keyPath = (string)javaRegKey.OpenSubKey(_rawJavaVersion)!.GetValue("JavaHome")!;
        string subJavaPath = Path.Combine(keyPath, "bin\\java.exe");

        // This is a VERY rare case
        if (!File.Exists(subJavaPath))
        {
            logger?.LogError($"Java version {_rawJavaVersion} found, but the Java directory it points to does not exist: {subJavaPath}");
            return false;
        }

        logger?.Log($"Using Java version {_rawJavaVersion} at {subJavaPath}");
        javaPath = subJavaPath;
        return true;
    }

    private static bool VerifyRawJavaVersion(string versionStr)
    {
        bool supportedVersion =
            // Java versions 8-22
            (Version.TryParse(versionStr, out Version? jdkVersion)
                && jdkVersion.Major == 1
                && jdkVersion.Minor >= 8)
            // Formatting for Java versions 23+ (or the JAVA_HOME path)
            || (int.TryParse(versionStr.Split('.')[0], out int major) && major >= 9);

        if (supportedVersion)
        {
            // Keep the successful version
            RawJavaVersion = versionStr;
        }

        return supportedVersion;
    }

    private static void LogInvalidInstallation(string javaPath)
    {
        StringBuilder logBuffer = new();
        bool binExists = Directory.Exists($"{javaPath}\\bin");

        _ = logBuffer.Append("JAVA_HOME is set to '")
            .Append(javaPath).Append("', but does not have java.exe. bin\\: does ");

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
