using Newtonsoft.Json;

namespace APKognito.Models;

public sealed class ExtraPackageFile
{
    [JsonProperty("file_path")]
    public string FilePath { get; set; } = string.Empty;

    [JsonProperty("file_type")]
    public FileType FileType { get; set; } = FileType.RegularText;
}

public enum FileType
{
    RegularText,
    Archive,
    Elf,
    RawBinary,
}