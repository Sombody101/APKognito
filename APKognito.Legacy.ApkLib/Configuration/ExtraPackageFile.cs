namespace APKognito.Legacy.ApkLib.Configuration;

public sealed class ExtraPackageFile
{
    public string FilePath { get; set; } = string.Empty;

    public FileType FileType { get; set; } = FileType.RegularText;
}

public enum FileType
{
    RegularText,
    Archive,
    Elf,
    RawBinary,
}
