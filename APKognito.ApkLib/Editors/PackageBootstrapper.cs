using System.Text;
using System.Xml;
using APKognito.ApkLib.Configuration;
using Microsoft.Extensions.Logging;

namespace APKognito.ApkLib.Editors;

public sealed class PackageBootstrapper
{
    private readonly BootstrapConfiguration _configuration;

    private readonly string _packagePath;
    private readonly XmlDocument _manifest;

    private readonly XmlNamespaceManager _namespace;

    private readonly ILogger _logger;

    private XmlNode ManifestRoot => _manifest.DocumentElement ?? throw new InvalidOperationException("A manifest must be loaded before any bootstrap injections.");

    public PackageBootstrapper(string packagePath, BootstrapConfiguration configuration, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(packagePath);
        ArgumentNullException.ThrowIfNull(configuration);

        string manifestPath = Path.Combine(packagePath, "AndroidManifest.xml");

        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("Failed to find the package manifest.", packagePath);
        }

        _manifest = new XmlDocument();
        _namespace = new(_manifest.NameTable);
        _namespace.AddNamespace("android", "http://schemas.android.com/apk/res/android");

        _packagePath = packagePath;
        _configuration = configuration;
        _logger = logger;

        _manifest.Load(manifestPath);
    }

    public async Task RunAsync()
    {
        XmlNode originalActivity = GetOriginalMainActivity(ManifestRoot["application"]!);

        OverwritePackageName();
        CreateBootstrapperActivity(originalActivity);

        string targetActivityPath = originalActivity.Attributes!["android:name"]!.Value;
        InjectBootstrapperClass(targetActivityPath);

        Save();
    }

    private void OverwritePackageName()
    {
        var rootElement = (XmlElement)ManifestRoot;
        XmlAttribute? packageName = rootElement.GetAttributeNode("package");

        _logger.LogInformation("Changing package name from {Name} to {NewName}", packageName!.Value, _configuration.NewPackageName);

        packageName!.Value = _configuration.NewPackageName;
    }

    private void InjectBootstrapperClass(string targetActivityPath)
    {
        string friendlyName = _configuration.FriendlyAppName ?? GetPackageFriendlyName();
        _logger.LogInformation("Using application friendly name '{Name}'", friendlyName);

        string bootstrapperClass = GetBootstrapperClass()
            .Replace("LBOOTSTRAP/PACKAGE", $"L{_configuration.NewPackageName.Replace('.', '/')}")
            .Replace("{NEW_PACKAGE_NAME}", _configuration.NewPackageName)
            .Replace("{BOOTSTRAP_TARGET_ACTIVITY}", targetActivityPath)
            .Replace("{FRIENDLY_APP_NAME}", friendlyName)
            .Replace("{ENABLE_CRASH_REPORTING}", _configuration.EnableErrorReporting.ToString().ToLower());

        string bootstrapDirectory = Path.Combine(_packagePath, "smali", _configuration.NewPackageName.Replace('.', Path.DirectorySeparatorChar));
        _ = Directory.CreateDirectory(bootstrapDirectory);

        _ = File.WriteAllTextAsync(Path.Combine(bootstrapDirectory, "Bootstrap.smali"), bootstrapperClass);
    }

    private void Save()
    {
        _manifest.Save(Path.Combine(_packagePath, "AndroidManifest.xml"));
    }

    private void CreateBootstrapperActivity(XmlNode originalActivity)
    {
        XmlNode application = ManifestRoot["application"]!;
        RemoveLauncherIntents(originalActivity);
        string bootstrapperActivity = GetBootstrapperActivity().Replace("{NEW_PACKAGE_NAME}", _configuration.NewPackageName);

        _logger.LogInformation("Injecting bootstrapper activity.");
        XmlDocumentFragment newActivity = _manifest.CreateDocumentFragment();
        newActivity.InnerXml = bootstrapperActivity;
        _ = application.PrependChild(newActivity);
    }

    private XmlNode GetOriginalMainActivity(XmlNode application)
    {
        XmlNode? foundActivity = application.SelectSingleNode("//activity[intent-filter/action[@android:name='android.intent.action.MAIN'] and intent-filter/category[@android:name='android.intent.category.LAUNCHER']]", _namespace);

        if (foundActivity is not null)
        {
            // This comment stops the formatter from making an ugly ternary
            return foundActivity;
        }

        foundActivity = application.SelectSingleNode("//activity[intent-filter/action[@android:name='android.intent.action.MAIN']]", _namespace);

        if (foundActivity is not null)
        {
            _logger.LogWarning("Failed to find a launcher activity, but main is present. This application will likely be hidden once installed.");
            return foundActivity;
        }

        throw new InvalidOperationException("Failed to find the original main activity.");
    }

    private void RemoveLauncherIntents(XmlNode node)
    {
        node.SelectSingleNode("//intent-filter/action[@android:name='android.intent.action.MAIN']", _namespace)?.Remove();
        node.SelectSingleNode("//intent-filter/category[@android:name='android.intent.category.LAUNCHER']", _namespace)?.Remove();
    }

    private string GetPackageFriendlyName()
    {
        const string NO_NAME_FOUND = "[no name found]";

        try
        {
            string appName = ManifestRoot["application"]!.Attributes!["android:label"]!.Value;

            if (!appName.StartsWith("@string"))
            {
                // Not likely to happen as manifests are compiler generated
                return appName;
            }

            return ExtractAppName(Path.Combine(_packagePath, "res/values/strings.xml")) ?? NO_NAME_FOUND;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to find application name! No name will be displayed on crash dialogs.");
            _logger.LogDebug(ex, "Failed to find application friendly name.");
            return NO_NAME_FOUND;
        }

        static string? ExtractAppName(string filePath)
        {
            using XmlReader reader = XmlReader.Create(filePath);

            while (reader.Read())
            {
                if (reader.NodeType is not XmlNodeType.Element || !reader.Name.Equals("string", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? nameAttribute = reader.GetAttribute("name");

                if (nameAttribute is not null && nameAttribute.Equals("app_name", StringComparison.OrdinalIgnoreCase))
                {
                    return reader.Read() && reader.NodeType is XmlNodeType.Text ? reader.Value : string.Empty;
                }
            }

            return null;
        }
    }

    private static string GetBootstrapperActivity()
    {
        return Encoding.Default.GetString(BootstrapperResources.BootstrapActivity);
    }

    private static string GetBootstrapperClass()
    {
        return Encoding.Default.GetString(BootstrapperResources.Bootstrap);
    }
}

internal static class BootstrapperExtentions
{
    public static void Remove(this XmlNode node)
    {
        _ = node.ParentNode!.RemoveChild(node);
    }
}
