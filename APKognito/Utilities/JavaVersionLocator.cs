using APKognito.Utilities.MVVM;
using Microsoft.Win32;
using System.IO;
using System.Text;

namespace APKognito.Utilities;

internal static class JavaVersionLocator
{
    private static string rawJavaVersion = string.Empty;
    private static LoggableObservableObject? logger = null;

    public static string? JavaExecutablePath { get; private set; } = null;
    public static bool JavaIsUpToDate { get; private set; } = false;

    /// <summary>
    /// Gets the Java path (JDK or JRE) that is Java Language 8+, and returns <see langword="true"/>. Otherwise, retuning <see langword="false"/> and <paramref name="javaPath"/>
    /// as <see langword="null"/>
    /// </summary>
    /// <param name="javaPath"></param>
    /// <param name="_logger"></param>
    /// <param name="skipIfSet"></param>
    /// <returns></returns>
    public static bool GetJavaPath(out string? javaPath, LoggableObservableObject? _logger = null, bool skipIfSet = true)
    {
        logger = _logger;

        if (skipIfSet && JavaIsUpToDate && File.Exists(JavaExecutablePath))
        {
            // Java already found
            _logger?.Log($"Using cached Java version {rawJavaVersion}");

            javaPath = JavaExecutablePath;
            return true;
        }

        // Get Java path
        JavaIsUpToDate = VerifyJavaInstallation(out javaPath);
        JavaExecutablePath = javaPath;

        FileLogger.Log($"Using Java version {rawJavaVersion}");

        return JavaIsUpToDate;
    }

    private static bool VerifyJavaInstallation(out string? javaPath)
    {
        javaPath = null;

        // Check for JDK via registry
        try
        {
            if (GetKey(Registry.LocalMachine.OpenSubKey("SOFTWARE\\JavaSoft\\Java Runtime Environment"), out javaPath, "JRE")
                || GetKey(Registry.LocalMachine.OpenSubKey("SOFTWARE\\JavaSoft\\JDK"), out javaPath))
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
            if (CheckJavaHome(out javaPath))
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
        }

        // Nothing found, tell user and yeet false
        logger?.LogError("Failed to find a valid JDK/JRE installation!\nYou can install the latest JDK version from here: https://www.oracle.com/java/technologies/downloads/?er=221886#jdk23-windows");
        logger?.LogError("If you know you have a Java installation, set your JAVA_HOME environment variable to the proper path for your Java installation.");
        return false;
    }

    [SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded", Justification = "nuh uh")]
    private static bool CheckLtsDirectory(out string? javaPath)
    {
        javaPath = null;

        FileLogger.Log("Checking Java latest directory...");

        try
        {
            // Check for 'latest' directory, use the shortcut to the latest JRE version.
            string[] dirs = Directory.GetDirectories(@"C:\Program Files\Java\latest");
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

        StringBuilder logBuffer = new();
        bool binExists = Directory.Exists($"{javaHome}\\bin");

        logBuffer.Append("JAVA_HOME is set to '")
            .Append(javaHome).Append("', but does not have java.exe. bin\\: does ");

        if (!binExists)
        {
            logBuffer.Append("not ");
        }

        logBuffer.Append("exist.");

        if (binExists)
        {
            logBuffer.AppendLine(" Files found in bin\\:");
            foreach (var file in Directory.GetFiles(javaHome))
            {
                logBuffer.Append('\t').AppendLine(Path.GetFileName(file));
            }
        }

        FileLogger.LogError(logBuffer.ToString());
        return false;
    }

    private static bool GetKey(RegistryKey? javaJdkKey, out string? javaPath, string javaType = "JDK")
    {
        javaPath = null;

        if (javaJdkKey is null)
        {
            return false;
        }

        if (javaJdkKey.GetValue("CurrentVersion") is not string _rawJavaVersion)
        {
            logger?.LogWarning($"A {javaType} installation key was found, but there was no Java version associated with it. Did a Java installation or uninstallation not complete correctly?");
            return false;
        }

        rawJavaVersion = _rawJavaVersion;

        if (!VerifyRawJavaVersion(_rawJavaVersion))
        {
            logger?.LogWarning($"{javaType} installation found with the version {_rawJavaVersion}, but it's not Java 8+");
            return false;
        }

        string keyPath = (string)javaJdkKey.OpenSubKey(_rawJavaVersion)!.GetValue("JavaHome")!;
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
            rawJavaVersion = versionStr;
        }

        return supportedVersion;
    }
}
