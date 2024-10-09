using APKognito.Configurations;
using APKognito.Views.Pages;
using MemoryPack;
using System.IO;

namespace APKognito.Models;

[MemoryPackable]
public partial record RenameSession
{
    [MemoryPackIgnore]
    public static readonly RenameSession Empty = new();

    private const string separator = ";;";

    public string[] ApkTransforms { get; }

    public long DateEpochSeconds { get; }

    [MemoryPackIgnore]
    public string FormattedDate => DateTimeOffset.FromUnixTimeSeconds(DateEpochSeconds).DateTime.ToString();

    [MemoryPackConstructor]
    public RenameSession(string[] apkTransforms, long dateEpochSeconds)
    {
        ApkTransforms = apkTransforms;
        DateEpochSeconds = dateEpochSeconds;
    }

    private RenameSession()
    {
        ApkTransforms = ["<>"];
        DateEpochSeconds = 0;
    }

    public static string FormatForSerializer(string str1, string str2, bool wasSuccess = true)
    {
        return $"{(wasSuccess ? 1 : 0)}{separator}{str1}{separator}{str2}";
    }

    public static string[] FormatForView(string str)
    {
        string[] parts = str.Split(separator);

        parts[0] = parts[0] is "1"
            ? "✔️"
            : "❌";

        return parts;
    }
}