using System.IO;
using Microsoft.Win32;

namespace APKognito.Utilities.JavaTools;

public record JavaVersionInformation
{
    public string JavaPath { get; init; }

    public Version Version { get; init; }

    public string RawVersion { get; init; }

    public JavaType JavaType { get; set; }

    public JavaVersionInformation(string javaPath, Version version, string rawVersion)
    {
        JavaPath = javaPath;
        Version = version;
        RawVersion = rawVersion;
    }

    internal JavaVersionInformation(RegistryKey key)
    {
        string homePath = (key.GetValue("JavaHome") as string)! ?? throw new InvalidJavaRegistryException("The JavaHome");
        JavaPath = Path.Combine(homePath, "bin\\java.exe");

        string keyName = Path.GetFileName(key.Name);
        if (!Version.TryParse(TrimVersionString(keyName), out Version? version))
        {
            throw new InvalidJavaRegistryException($"Invalid and untrimmable Java version '{key.Name}'");
        }

        Version = version;
        RawVersion = keyName;

        if (key.Name.Contains("JDK"))
        {
            JavaType = JavaType.JDK;
        }
        else if (key.Name.Contains("Java Runtime Environment"))
        {
            JavaType = JavaType.JRE;
        }
    }

    public bool UpToDate => VersionUpToDate(Version, RawVersion);

    public string FixedRawVersion => TrimVersionString(RawVersion);

    public static bool VersionUpToDate(Version version, string rawVersion)
    {
        return
            // Java versions 8-22
            version.Major == 1 && version.Minor >= 8
            // Formatting for Java versions 23+ (or the JAVA_HOME path)
            || int.TryParse(rawVersion.Split('.')[0], out int major) && major >= 9;
    }

    public override string ToString()
    {
        return $"{JavaType} {Version} ({RawVersion})";
    }

    private static string TrimVersionString(string rawVersion)
    {
        int underscoreIndex = rawVersion.IndexOf('_');

        return underscoreIndex is not -1
            ? rawVersion[..underscoreIndex]
            : rawVersion;
    }

    public sealed class InvalidJavaRegistryException(string message) : Exception(message)
    {
    }
}

public enum JavaType
{
    Unknown,
    JDK,
    JRE
}
