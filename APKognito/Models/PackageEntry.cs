using APKognito.Helpers;
using System.Text;

namespace APKognito.Models;

public class PackageEntry
{
    private const char ARG_SEPARATOR = '|';

    public string PackageName { get; }

    public string PackagePath { get; }

    public string? AssetPath { get; }

    public long PackageSizeBytes { get; }

    public long AssetsSizeBytes { get; }

    public long SaveDataSizeBytes { get; }

    public PackageEntry(string packageName, string packagePath, long packageSizeBytes, string? assetPath, long assetsSizeBytes, long saveDataSizeBytes)
    {
        PackageName = packageName;
        PackagePath = packagePath;
        AssetPath = string.IsNullOrWhiteSpace(assetPath)
            ? null
            : assetPath;
        PackageSizeBytes = packageSizeBytes;
        AssetsSizeBytes = assetsSizeBytes;
        SaveDataSizeBytes = saveDataSizeBytes;
    }

    public string FormattedAssetsSize => AssetsSizeBytes < 0
        ? "(no assets)"
        : GBConverter.FormatSizeFromBytes(AssetsSizeBytes);

    public string FormattedPackageSize => GBConverter.FormatSizeFromBytes(PackageSizeBytes);

    public string FormattedSaveDataSize => SaveDataSizeBytes < 0
        ? "(no save data)"
        : GBConverter.FormatSizeFromBytes(SaveDataSizeBytes);

    public string FormattedTotalSize => GBConverter.FormatSizeFromBytes(PackageSizeBytes 
        + Math.Max(AssetsSizeBytes, 0) 
        + Math.Max(SaveDataSizeBytes, 0));

    public override string ToString()
    {
        StringBuilder sb = new(PackageName);

        if (AssetPath is not null)
        {
            sb.Append(" (assets ")
                .Append(GBConverter.FormatSizeFromBytes(AssetsSizeBytes))
                .Append(')');
        }

        return sb.ToString();
    }

    public static PackageEntry ParseEntry(string adbPackage)
    {
        // <package name>|<package path>|<package size in bytes>|<assets size in bytes>|<save data size in bytes>
        string[] split = adbPackage.Split(ARG_SEPARATOR);
        if (split.Length != 5)
        {
            return new PackageEntry("[Invalid Format]", string.Empty, -1, null, -1, -1);
        }

        string packageName = split[0];

        string packagePath = split[1];

        if (!long.TryParse(split[2], out long packageSize))
        {
            packageSize = -1;
        }

        if (!long.TryParse(split[3], out long assetsSize))
        {
            assetsSize = -1;
        }

        if (!long.TryParse(split[4], out long saveDataSize))
        {
            saveDataSize = -1;
        }

        string? assetsPath = assetsSize is -1
            ? null
            : $"/sdcard/Android/obb/{packageName}";

        return new PackageEntry(packageName, packagePath, packageSize * 1024, assetsPath, assetsSize * 1024, saveDataSize * 1024);
    }
}
