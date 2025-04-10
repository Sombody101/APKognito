using System.Diagnostics;

namespace APKognito.AdbTools;

public readonly struct CommandOutput : ICommandOutput
{
    public readonly string StdOut { get; }

    public readonly string StdErr { get; }

    public readonly bool Errored => !string.IsNullOrWhiteSpace(StdErr);

    public readonly void ThrowIfError(bool noThrow = false, int? exitCode = null)
    {
        if (!noThrow && Errored && exitCode is not null && exitCode.Value is not 0)
        {
            throw new CommandException(StdErr);
        }
    }

    public CommandOutput(string stdout, string stderr)
    {
        StdOut = stdout;
        StdErr = stderr;
    }

    public static async Task<CommandOutput> GetCommandOutputAsync(Process proc)
    {
        return new(
            await proc.StandardOutput.ReadToEndAsync(),
            await proc.StandardError.ReadToEndAsync()
        );
    }

    public class CommandException : Exception
    {
        public CommandException(string error)
            : base(error)
        { }
    }
}