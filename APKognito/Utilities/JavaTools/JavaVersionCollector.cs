using Microsoft.Win32;

namespace APKognito.Utilities.JavaTools;

public static class JavaVersionCollector
{
    private const string JDK_HIVE_PATH = "SOFTWARE\\JavaSoft\\JDK";
    private const string JRE_HIVE_PATH = "SOFTWARE\\JavaSoft\\Java Runtime Environment";

    private static readonly ICollection<JavaVersionInformation> s_knownJavaVersions = [];

    public static IReadOnlyCollection<JavaVersionInformation> JavaVersions => (IReadOnlyCollection<JavaVersionInformation>)s_knownJavaVersions;

    static JavaVersionCollector()
    {
        _ = RefreshJavaVersions();
    }

    public static JavaVersionInformation GetVersion(string? wantedRawVersion)
    {
        IReadOnlyCollection<JavaVersionInformation> javaVersions = JavaVersions;
        if (javaVersions.Count is 0)
        {
            throw new NoJavaInstallationsException();
        }

        return !string.IsNullOrWhiteSpace(wantedRawVersion)
            ? javaVersions.First(v => v.RawVersion == wantedRawVersion)!
            : javaVersions.First();
    }

    public static IReadOnlyCollection<JavaVersionInformation> RefreshJavaVersions()
    {
        s_knownJavaVersions.Clear();

        foreach (RegistryKey key in GetJavaKeys())
        {
            try
            {
                s_knownJavaVersions.Add(new JavaVersionInformation(key));
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex);
            }
        }

        return JavaVersions;
    }

    public static IEnumerable<RegistryKey> GetJavaKeys()
    {
        List<RegistryKey> foundKeys = [];

        AddChildrenKeys(JDK_HIVE_PATH);
        AddChildrenKeys(JRE_HIVE_PATH);

        return foundKeys;

        void AddChildrenKeys(string hivePath)
        {
            using RegistryKey? parentKey = Registry.LocalMachine.OpenSubKey(hivePath);

            if (parentKey is null)
            {
                return;
            }

            foreach (string subkeyName in parentKey.GetSubKeyNames().AsEnumerable().Reverse())
            {
                RegistryKey subkey = parentKey.OpenSubKey(subkeyName)!;
                foundKeys.Add(subkey);
            }
        }
    }

    public class NoJavaInstallationsException() : Exception("No JDK or JRE installations found.")
    {
    }
}
