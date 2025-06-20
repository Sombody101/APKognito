using System.Diagnostics;

namespace APKognito.ApkLib;

public static class JavaCreator
{
    /// <summary>
    /// Creates a process, best suited for Java invocations, or other CLI tools.
    /// </summary>
    /// <param name="filename"></param>
    /// <returns></returns>
    public static Process CreateManualProcess(string filename, IEnumerable<string>? args = null)
    {
        var cliProc = new Process()
        {
            StartInfo = new()
            {
                FileName = filename,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        if (args is not null && args.Any())
        {
            foreach (string arg in args)
            {
                cliProc.StartInfo.ArgumentList.Add(arg);
            }
        }

        return cliProc;
    }
}
