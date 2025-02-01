using APKognito.Helpers;
using System.Text;

namespace APKognito.Models;

public class PackageEntry
{
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

    public string FormattedAssetsSize
    {
        get
        {
            if (AssetsSizeBytes < 0)
            {
                return "(no assets)";
            }

            return AdbGBConverter.FormatSizeFromBytes(AssetsSizeBytes);
        }
    }

    public string FormattedPackageSize => AdbGBConverter.FormatSizeFromBytes(PackageSizeBytes);

    public string FormattedSaveDataSize
    {
        get
        {
            if (SaveDataSizeBytes < 0)
            {
                return "(no save data)";
            }

            return AdbGBConverter.FormatSizeFromBytes(SaveDataSizeBytes);
        }
    }

    public string FormattedTotalSize
    {
        get
        {
            long assetsSize = AssetsSizeBytes < 0
                ? 0
                : AssetsSizeBytes;

            long saveDataSize = SaveDataSizeBytes < 0
                ? 0
                : SaveDataSizeBytes;

            return AdbGBConverter.FormatSizeFromBytes(PackageSizeBytes + assetsSize + saveDataSize);
        }
    }

    public override string ToString()
    {
        StringBuilder sb = new(PackageName);

        if (AssetPath is not null)
        {
            sb.Append(" (assets ")
                .Append(AssetsSizeBytes is 0
                    ? 0
                    : AssetsSizeBytes / 1024 / 1024)
                .Append(" MB )");
        }

        return sb.ToString();
    }

    public static PackageEntry ParseEntry(string adbPackage)
    {
        // <package name>|<package path>|<package size in bytes>|<assets size in bytes>|<save data size in bytes>
        string[] split = adbPackage.Split('|');
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
