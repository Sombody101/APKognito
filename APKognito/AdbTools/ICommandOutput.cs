namespace APKognito.AdbTools;

public interface ICommandOutput
{
    public string StdOut { get; }
    public string StdErr { get; }

    public bool Errored { get; }

    public void ThrowIfError(bool noThrow = false, int? exitCode = null);
}