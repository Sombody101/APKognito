using APKognito.Helpers;
using Newtonsoft.Json;

namespace APKognito.Models;

public sealed record PackageEntry
{
    private const char ARG_SEPARATOR = '|';

    [JsonProperty("package_name")]
    public string PackageName { get; }

    [JsonProperty("package_path")]
    public string PackagePath { get; }

    public string? AssetPath { get; }

    [JsonProperty("package_size")]
    public long PackageSizeBytes { get; }

    [JsonProperty("assets_size")]
    public long AssetsSizeBytes { get; }

    [JsonProperty("data_size")]
    public long SaveDataSizeBytes { get; }

    public PackageEntry(string packageName, string packagePath, long packageSizeBytes, string? assetPath, long assetsSizeBytes, long saveDataSizeBytes)
    {
        PackageName = packageName;
        PackagePath = packagePath;
        AssetPath = string.IsNullOrWhiteSpace(assetPath)
            ? null
            : assetPath;
        PackageSizeBytes = packageSizeBytes * 1024;
        AssetsSizeBytes = assetsSizeBytes * 1024;
        SaveDataSizeBytes = saveDataSizeBytes * 1024;
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
