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

public static class RenameSessionManager
{
    private static readonly string historyFilePath;

    private static List<RenameSession>? _renameSessions;

    static RenameSessionManager()
    {
        string configsPath = Path.Combine(App.AppData!.FullName, "./config");
        historyFilePath = Path.Combine(configsPath, "history.bin");

        if (File.Exists("./config/history.bin") && !File.Exists(historyFilePath))
        {
            File.Move("./config/history.bin", historyFilePath);
        }
    }

    public static List<RenameSession> GetSessions()
    {
        if (_renameSessions is null)
        {
            LoadSessions();
        }

        return _renameSessions;
    }

    /* 
     * This is the most newbie ass implementation ever 
     * But is it going to stay regardless? Yes. Yes it is.
     */

    public static void LoadSessions()
    {
        if (!File.Exists(historyFilePath) || new FileInfo(historyFilePath).Length == 0)
        {
            _renameSessions = [];
            return;
        }

        byte[] packed = File.ReadAllBytes(historyFilePath);

        List<RenameSession>? deserialized = MemoryPackSerializer.Deserialize<List<RenameSession>>(packed);

        if (deserialized is null)
        {
            Wpf.Ui.Controls.MessageBox errorBox = new()
            {
                Title = "Failed to load rename history!",
                Content = "Unable to load rename history. Unknown error."
            };

            errorBox.ShowDialogAsync().Wait();
        }
        else
        {
            _renameSessions = deserialized;
        }
    }

    public static void SaveSessions()
    {
        byte[] packed = MemoryPackSerializer.Serialize(_renameSessions);

        File.WriteAllBytes(historyFilePath, packed);
    }
}