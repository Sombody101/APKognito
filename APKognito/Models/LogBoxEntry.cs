using System.Windows.Media;

namespace APKognito.Models;

public class LogBoxEntry
{
    public string Text { get; set; } = string.Empty;

    public Brush? Color { get; set; }

    public LogEntryType? LogType { get; set; }

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
