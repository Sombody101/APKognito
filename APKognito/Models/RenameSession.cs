using MemoryPack;

namespace APKognito.Models;

[MemoryPackable]
public partial record RenameSession
{
    private const string SEPARATOR = ";;";

    [MemoryPackIgnore]
    public static readonly RenameSession Empty = new();

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

#if DEBUG
    public RenameSession((bool success, string originalName, string finalName)[] elements, long epoch)
    {
        DateEpochSeconds = epoch;

        List<string> transforms = [];

        foreach ((bool success, string originalName, string finalName) in elements)
        {
            int iSuccess = success ? 1 : 0;
            transforms.Add($"{iSuccess}{SEPARATOR}{originalName}{SEPARATOR}{finalName}");
        }

        ApkTransforms = [.. transforms];
    }
#endif

    private RenameSession()
    {
        ApkTransforms = ["<>"];
        DateEpochSeconds = 0;
    }

    public static string FormatForSerializer(string str1, string str2, bool wasSuccess = true)
    {
        return $"{(wasSuccess ? 1 : 0)}{SEPARATOR}{str1}{SEPARATOR}{str2}";
    }
    
    public static string[] FormatForView(string str)
    {
        string[] parts = str.Split(SEPARATOR);

        parts[0] = parts[0] is "1"
            ? "✔️"
            : "❌";

        return parts;
    }
}