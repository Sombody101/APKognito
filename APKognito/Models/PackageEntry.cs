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
}
