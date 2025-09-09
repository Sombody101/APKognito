using System.Runtime.CompilerServices;

namespace APKognito.ApkLib.Utilities;

public static class FunctionalGrep
{
    public static async IAsyncEnumerable<string> FindFilesWithSubstringAsync(
        string substring,
        IEnumerable<string> directories,
        IProgress<ProgressInfo>? reporter,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        int scannedCount = 0;

        foreach (string directory in directories)
        {
            token.ThrowIfCancellationRequested();

            IEnumerable<string> files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories);

            foreach (string filePath in files)
            {
                scannedCount++;
                token.ThrowIfCancellationRequested();
                reporter.ReportProgressTitle("Scanning");
                reporter.ReportProgressMessage($"{scannedCount}: {Path.GetFileName(filePath)}");

                if (await ContainsSubstringInFileAsync(filePath, substring, token))
                {
                    yield return filePath;
                }
            }
        }
    }

    private static async Task<bool> ContainsSubstringInFileAsync(string filePath, string substring, CancellationToken token)
    {
        const int bufferSize = 4096 * 4;
        char[] buffer = new char[bufferSize];
        string remaining = string.Empty;

        using (var reader = new StreamReader(filePath))
        {
            int bytesRead;
            while ((bytesRead = await reader.ReadAsync(buffer.AsMemory(), token)) > 0)
            {
                token.ThrowIfCancellationRequested();

                string chunk = new(buffer, 0, bytesRead);
                string combinedChunk = remaining + chunk;

                if (combinedChunk.Contains(substring))
                {
                    return true;
                }

                remaining = combinedChunk.Substring(Math.Max(0, combinedChunk.Length - substring.Length - 1));
            }
        }

        return false;
    }
}
