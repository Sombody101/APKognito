using APKognito.Utilities;
using System.Windows.Media;

namespace APKognito.Models;

public class LogBoxEntry
{
    public string Text { get; init; } = string.Empty;

    public Brush? Color { get; init; }

    public LogEntryType? LogType { get; init; }

    public enum LogEntryType
    {
        None,
        Info,
        Success,
        Warning,
        Error,
        Debug,
    }

    public static LogEntryType ConvertLogLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.INFO => LogEntryType.Info,
            LogLevel.ERROR or LogLevel.FATAL => LogEntryType.Error,
            LogLevel.DEBUG or LogLevel.TRACE => LogEntryType.Debug,
            LogLevel.WARNING => LogEntryType.Warning,
            LogLevel.NONE => LogEntryType.None,

            // This might break things... Only time will tell.
            _ => LogEntryType.Success
        };
    }
}
