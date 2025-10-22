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

    public static async Task<ulong> GetDirectorySizeAsync(string directory, CancellationToken token = default)
    {
        return await GetDirectorySizeAsync(new DirectoryInfo(directory), token);
    }

    public static async Task<ulong> GetDirectorySizeAsync(DirectoryInfo directoryInfo, CancellationToken cancellation = default)
    {
        List<Task<ulong>> tasks = [];
        foreach (FileInfo fi in directoryInfo.GetFiles())
        {
            tasks.Add(Task.Run(() => (ulong)fi.Length, cancellation));
        }

        foreach (DirectoryInfo directory in directoryInfo.GetDirectories())
        {
            tasks.Add(GetDirectorySizeAsync(directory, cancellation));
        }

        ulong[] results = await Task.WhenAll(tasks);

        return results.Length is not 0
            ? results.Aggregate((a, c) => a + c)
            : 0;
    }

    public static bool TryGetClaimFile(string directory, [NotNullWhen(true)] out string? claimFile, string claimName = CLAIM_FILE_NAME)
    {
        claimFile = GetClaimFile(directory, claimName);
        return claimFile is not null;
    }

    // This method doesn't accept a 'recursive' bool because it's implied when called.
    // If this is called on a directory that wasn't meant to be recursively removed, that's on you.
    public static async Task DeleteDirectoryAsync(string directory, CancellationToken token = default)
    {
        if (!Directory.Exists(directory))
        {
            throw new IOException($"Failed to locate directory: ${directory}");
        }

        await DeleteDirectoryInternalAsync(directory, token);
    }

    private static async Task DeleteDirectoryInternalAsync(string directory, CancellationToken token)
    {
        foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (File.Exists(entry))
            {
                File.Delete(entry);
            }
            else
            {
                // Let's hope it wasn't already deleted by something else, and that the caller can handle the exception, cause we ain't.
                await DeleteDirectoryInternalAsync(entry, token);
            }
        }

        if (token.IsCancellationRequested)
        {
            return;
        }

        Directory.Delete(directory);
    }
}
