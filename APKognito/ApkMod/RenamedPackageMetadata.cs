using Newtonsoft.Json;

namespace APKognito.ApkMod;

public sealed record RenamedPackageMetadata
{
    [JsonProperty("pack_name"), JsonRequired]
    public required string PackageName { get; set; }

    [JsonProperty("orig_pack_name")]
    public string OriginalPackageName { get; set; } = "io.unknown.unknown";

    [JsonProperty("rel_asset_path")]
    public string? RelativeAssetsPath { get; set; }

    [JsonProperty("date")]
    public DateTimeOffset RenameDate { get; set; }

    [JsonProperty("kognito_version"), JsonRequired]
    public required Version ApkognitoVersion { get; set; }
}
