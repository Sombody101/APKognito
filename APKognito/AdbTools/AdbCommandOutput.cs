using System.Diagnostics;
using System.Text;

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

    public override string ToString()
    {
        StringBuilder sb = new();

        sb.Append(nameof(StdOut)).Append(": ").AppendLine(StdOut);
        sb.Append(nameof(StdErr)).Append(": ").AppendLine(StdErr);
        sb.Append(nameof(Errored)).Append(": ").AppendLine(Errored.ToString());
        sb.Append(nameof(DeviceNotAuthorized)).Append(": ").AppendLine(DeviceNotAuthorized.ToString());

        return sb.ToString();
    }

    public static async Task<AdbCommandOutput> GetCommandOutputAsync(Process proc)
    {
        return new(
            await proc.StandardOutput.ReadToEndAsync(),
            await proc.StandardError.ReadToEndAsync()
        );
    }

    public class AdbCommandException(string error) : Exception(error)
    {
    }
}
