using System.Diagnostics;

namespace APKognito.AdbTools;

public record AdbCommandOutput : CommandOutput
{
    public bool DeviceNotAuthorized { get; }

    public AdbCommandOutput(string stdout, string stderr, int exitCode = 0)
        : base(stdout, stderr, exitCode)
    {
        if (Errored)
        {
            // Jank, but ADB isn't script friendly.
            DeviceNotAuthorized = StdErr.StartsWith("adb.exe: device unauthorized.");
        }
    }

    public static async Task<AdbCommandOutput> ReadCommandOutputAsync(Process proc, CancellationToken token = default)
    {
        // Google has decided that any kind of "diagnostic" information needs to be written to stderr. They actually fixed this at one point, but
        // the issue was reintroduced some time in 2024.
        // This makes it hard to determine if something is due to an actual error, or if it's what they determine as "diagnostic", which includes
        // knowing how many files were pushed to a device, even if there were no issues.

        // I don't expect anything great from them considering they don't even write progress updates to stdout or stderr, and decided to write
        // progress information directly to the console buffer to avoid flickering, as if there aren't easier ways around that.
        // **cough cough** turn off the fucking cursor **cough cough**

        return new(
            await proc.StandardOutput.ReadToEndAsync(token),
            await proc.StandardError.ReadToEndAsync(token)
        );
    }

    public override void ThrowIfError(bool noThrow = false, int? exitCode = null)
    {
        if (!noThrow && Errored)
        {
            throw new AdbCommandException(StdErr);
        }
    }

    public class AdbCommandException(string error) : Exception(error)
    {
    }
}
