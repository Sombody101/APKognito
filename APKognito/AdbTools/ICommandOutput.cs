using System.Diagnostics;

namespace APKognito.AdbTools;

internal interface ICommandOutput
{
    public string StdOut { get; }
    public string StdErr { get; }

    public bool Errored { get; }

    public void ThrowIfError(bool noThrow = false, int? exitCode = null);
}
