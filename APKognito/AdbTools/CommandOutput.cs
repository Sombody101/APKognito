using System.Diagnostics;

namespace APKognito.AdbTools;

public record CommandOutput : ICommandOutput
{
    public string StdOut { get; init; } = string.Empty;

    public string StdErr { get; init; } = string.Empty;

    public int ExitCode { get; init; } = 0;

    public bool Errored { get; init; }

    public CommandOutput(string stdout, string stderr, int exitCode = 0)
    {
        StdOut = stdout;
        StdErr = stderr;
        ExitCode = exitCode;

        Errored = !string.IsNullOrWhiteSpace(StdErr) && exitCode > 0;
    }

    protected CommandOutput()
    {
    }

    public static async Task<CommandOutput> GetCommandOutputAsync(Process proc)
    {
        return new(
            await proc.StandardOutput.ReadToEndAsync(),
            await proc.StandardError.ReadToEndAsync(),
            proc.ExitCode
        );
    }

    public virtual void ThrowIfError(bool noThrow = false, int? exitCode = null)
    {
        if (!noThrow && Errored)
        {
            throw new CommandException(StdErr);
        }
    }

    public class CommandException(string error) : Exception(error)
    {
    }
}
