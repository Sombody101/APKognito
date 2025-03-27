using System.Diagnostics;

namespace APKognito.AdbTools;

/// <summary>
/// Holds references to the STDOUT and STDERR output of an ADB command.
/// </summary>
public readonly struct AdbCommandOutput : ICommandOutput
{
    /// <summary>
    /// All text data from STDOUT of an ADB command.
    /// </summary>
    public readonly string StdOut { get; }

    /// <summary>
    /// All text data from STDERR of an ADB command.
    /// </summary>
    public readonly string StdErr { get; }

    public readonly bool Errored => !string.IsNullOrWhiteSpace(StdErr);

    public readonly bool DeviceNotAuthorized => StdErr.StartsWith("adb.exe: device unauthorized.");

    public readonly void ThrowIfError(bool noThrow = false, int? exitCode = null)
    {
        if (!noThrow && Errored && exitCode is not null && exitCode.Value is not 0)
        {
            throw new AdbCommandException(StdErr);
        }
    }

    public AdbCommandOutput(string stdout, string stderr)
    {
        StdOut = stdout;
        StdErr = stderr;
    }

    public static async Task<AdbCommandOutput> GetCommandOutput(Process proc)
    {
        return new(
            await proc.StandardOutput.ReadToEndAsync(),
            await proc.StandardError.ReadToEndAsync()
        );
    }

    public class AdbCommandException : Exception
    {
        public AdbCommandException(string error)
            : base(error)
        { }
    }
}