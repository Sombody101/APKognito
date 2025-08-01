using Microsoft.Win32;

namespace APKognito.Utilities.JavaTools;

public sealed class JavaVersionCollector
{
    private const string JDK_HIVE_PATH = "SOFTWARE\\JavaSoft\\JDK";
    private const string JRE_HIVE_PATH = "SOFTWARE\\JavaSoft\\Java Runtime Environment";

    private readonly ICollection<JavaVersionInformation> _knownJavaVersions = [];

    public IReadOnlyCollection<JavaVersionInformation> JavaVersions => (IReadOnlyCollection<JavaVersionInformation>)_knownJavaVersions;

    public JavaVersionCollector()
    {
        _ = CollectVersions();
    }

    public JavaVersionInformation GetVersion(string? wantedRawVersion)
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

    public IReadOnlyCollection<JavaVersionInformation> CollectVersions()
    {
        _knownJavaVersions.Clear();

        foreach (RegistryKey key in GetJavaKeys())
        {
            try
            {
                _knownJavaVersions.Add(new JavaVersionInformation(key));
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

            foreach (string subkeyName in parentKey.GetSubKeyNames())
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
