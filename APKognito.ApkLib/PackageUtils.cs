using System.Globalization;
using System.Xml;

namespace APKognito.ApkLib;

public static class PackageUtils
{
    /// <summary>
    /// Replaces the company name of a <paramref name="originalPackageName"/> package name with a <paramref name="newCompanyName"/>.
    /// </summary>
    /// <param name="originalPackageName"></param>
    /// <param name="newCompanyName"></param>
    /// <returns></returns>
    public static string ReplaceCompanyName(string originalPackageName, string newCompanyName)
    {
        if (string.IsNullOrEmpty(originalPackageName))
        {
            return newCompanyName;
        }

        int firstDotIndex = originalPackageName.IndexOf('.');

        if (firstDotIndex is -1)
        {
            return newCompanyName;
        }

        int secondDotIndex = originalPackageName.IndexOf('.', firstDotIndex + 1);

        return secondDotIndex is -1
            ? string.Concat(originalPackageName.AsSpan(0, firstDotIndex + 1), newCompanyName)
            : string.Concat(originalPackageName.AsSpan(0, firstDotIndex + 1), newCompanyName, originalPackageName.AsSpan(secondDotIndex));
    }

    /// <summary>
    /// Gets the company name from a Java package name.
    /// </summary>
    /// <param name="packageName"></param>
    /// <returns></returns>
    public static string GetPackageCompany(string packageName)
    {
        if (string.IsNullOrEmpty(packageName))
        {
            return string.Empty;
        }

        int firstDotIndex = packageName.IndexOf('.');

        /*
         * app => app
         * com.app => app
         * com.company.app => company
         * com.company.app.something... => company
         */

        if (firstDotIndex is -1)
        {
            return packageName;
        }

        int secondDotIndex = packageName.IndexOf('.', firstDotIndex + 1);

        return secondDotIndex is -1
            ? packageName.Substring(firstDotIndex + 1)
            : packageName.Substring(firstDotIndex + 1, secondDotIndex - firstDotIndex - 1);
    }

    /// <summary>
    /// Gets the package name from an AndroidManifest file using <paramref name="manifestPath"/>.
    /// </summary>
    /// <param name="manifestPath"></param>
    /// <returns></returns>
    public static string GetPackageName(string manifestPath)
    {
        using FileStream stream = File.OpenRead(manifestPath);
        return GetPackageName(stream);
    }

    /// <summary>
    /// Gets the package name from a stream, assuming <paramref name="fileStream"/> is a valid AndroidManifest XML file.
    /// </summary>
    /// <param name="fileStream"></param>
    /// <returns></returns>
    /// <exception cref="ManifestPackageNameMissingException"></exception>
    public static string GetPackageName(Stream fileStream)
    {
        XmlDocument xmlDoc = new();
        xmlDoc.Load(fileStream);

        return xmlDoc.DocumentElement?.Attributes["package"]?.Value
            ?? throw new ManifestPackageNameMissingException();
    }

    internal static string GetFormattedTimeDirectory(string sourceApkName)
    {
        // Use english culture info because different language selections from messing with output directories (apktool doesn't like it for some reason)
        // 🦅🦅 MERICA
        return $"{sourceApkName}_{DateTime.Now.ToString("yyyy-MM-dd_h.mm", CultureInfo.InvariantCulture)}";
    }

    public class ManifestPackageNameMissingException() : Exception("Failed to get package name from AndroidManifest (XML).");
}
