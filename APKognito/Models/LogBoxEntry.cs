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
}
