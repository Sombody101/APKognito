using System.IO;

namespace APKognito.Utilities;

internal static class DirectoryManager
{
    private const string CLAIM_FILE_NAME = ".apkognito";

    public static string CreateClaimedDirectory(string directory, string claimname = CLAIM_FILE_NAME)
    {
        _ = Directory.CreateDirectory(directory);
        return ClaimDirectory(directory, claimname);
    }

    public static string ClaimDirectory(string directory, string claimName = CLAIM_FILE_NAME)
    {
        if (!Directory.Exists(directory))
        {
            throw new ArgumentException($"Unable to claim directory '{directory}' as it doesn't exist.");
        }

        string hiddenFile = Path.Combine(directory, claimName);

        if (!File.Exists(hiddenFile))
        {
            File.Create(hiddenFile).Close();
            File.SetAttributes(hiddenFile, File.GetAttributes(hiddenFile) | FileAttributes.Hidden);
        }

        return hiddenFile;
    }

    public static bool IsDirectoryClaimed(string directory, string claimName = CLAIM_FILE_NAME)
    {
        return !Directory.Exists(directory)
            ? throw new ArgumentException($"Unable to check if directory is claimed '{directory}' as it doesn't exist.")
            : File.Exists(Path.Combine(directory, claimName));
    }

    public static string? GetClaimFile(string directory, string claimName = CLAIM_FILE_NAME)
    {
        string claimFile = Path.Combine(directory, claimName);
        return File.Exists(claimFile)
            ? claimFile
            : null;
    }

    public static async Task<ulong> DirSizeAsync(string directory, CancellationToken token = default)
    {
        return await DirSizeAsync(new DirectoryInfo(directory), token);
    }

    public static async Task<ulong> DirSizeAsync(DirectoryInfo d, CancellationToken cancellation = default)
    {
        List<Task<ulong>> tasks = [];
        foreach (FileInfo fi in d.GetFiles())
        {
            tasks.Add(Task.Run(() => (ulong)fi.Length, cancellation));
        }

        foreach (DirectoryInfo di in d.GetDirectories())
        {
            tasks.Add(DirSizeAsync(di, cancellation));
        }

        ulong[] results = await Task.WhenAll(tasks);

        return results.Length is not 0
            ? results.Aggregate((a, c) => a + c)
            : 0;
    }

    public static bool TryGetClaimFile(string directory, [NotNullWhen(true)] out string? claimFile, string claimName = CLAIM_FILE_NAME)
    {
        claimFile = GetClaimFile(directory);
        return claimFile is not null;
    }
}
