using MemoryPack;
using System.IO;

namespace APKognito.Models;

[MemoryPackable]
public partial record RenameSession
{
    private const string separator = ";;";

    public string[] ApkTransforms { get; }

    public long DateEpochSeconds { get; }

    [MemoryPackIgnore]
    public string FormattedDate => DateTimeOffset.FromUnixTimeSeconds(DateEpochSeconds).DateTime.ToString();

    public RenameSession(string[] apkTransforms, long dateEpochSeconds)
    {
        ApkTransforms = apkTransforms;
        DateEpochSeconds = dateEpochSeconds;
    }

    public static string FormatForSerializer(string str1, string str2, bool wasSuccess = true)
    {
        return $"{(wasSuccess ? 1 : 0)}{separator}{str1}{separator}{str2}";
    }

    public static string[] FormatForView(string str)
    {
        string[] parts = str.Split(separator);

        parts[0] = parts[0] == "1"
            ? "✔️"
            : "❌";

        return parts;
    }
}

public static class RenameSessionManager
{
    private const string historyFilePath = "./config/history.bin";

    private static List<RenameSession>? _renameSessions;

    public static List<RenameSession> GetSessions()
    {
        if (_renameSessions is null)
            LoadSessions();

        return _renameSessions;
    }

    /* 
     * This is the most newbie ass implementation ever 
     * But is it going to stay regardless? Yes. Yes it is.
     */

    public static void LoadSessions()
    {
        if (!File.Exists(historyFilePath))
        {
            File.Create(historyFilePath);
            _renameSessions = [];
            return;
        }

        // Previous session failed to save... Just create a new array and don't touch the file
        if (new FileInfo(historyFilePath).Length == 0)
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