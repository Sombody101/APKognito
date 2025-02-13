using APKognito.Utilities;
using System.Text.RegularExpressions;
using Wpf.Ui.Appearance;
using Brush = System.Windows.Media.Brush;

namespace APKognito.Models;

public partial class LogViewerLine
{
    private const string DEFAULT = "[None]";

    public string RawLog { get; }
    public bool HasException { get; }

    public string LogTime { get; private set; } = string.Empty;
    public string CallSite { get; private set; } = DEFAULT;
    public string LogMessage { get; private set; } = DEFAULT;
    public LogLevel LogLevel { get; private set; }
    public string? ExceptionLog { get; private set; }

    public Visibility ExceptionLogVisible => string.IsNullOrEmpty(ExceptionLog)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Brush Background => LogLevel switch 
    {
        LogLevel.INFO => ApplicationAccentColorManager.PrimaryAccentBrush,
        LogLevel.WARNING => 
    };

    public LogViewerLine(string log, bool hasExceptionLog)
    {
        RawLog = log;
        HasException = hasExceptionLog;

        ParseLog();
    }

    private void ParseLog()
    {
        if (string.IsNullOrEmpty(RawLog) || RawLog[0] is not '[')
        {
            throw new ArgumentException("Invalid raw log");
        }

        Match match = LogParseRegex().Match(RawLog);

        if (!match.Success)
        {
            throw new FormatException($"Log format does not match expected pattern: {RawLog}");
        }

        string timeString = match.Groups[1].Value;
        int lastSpace = timeString.LastIndexOf(' ') + 1;

        _ = Enum.TryParse(timeString[lastSpace..], out LogLevel level);
        LogLevel = level;
        timeString = timeString[..lastSpace];
        CallSite = match.Groups[2].Value;
        LogMessage = match.Groups[3].Value;

        if (HasException)
        {
            ExceptionLog = ParseExceptionLog(match.Groups[4].Value);
        }

        LogTime = timeString;
    }

    private static string ParseExceptionLog(string log)
    {
        const int colonLimit = 5;

        int prefixTrim = 0;
        int colonCount = 0;
        for (; prefixTrim < log.Length; prefixTrim++)
        {
            if (log[prefixTrim] is ':')
            {
                colonCount++;
            }

            if (colonCount is colonLimit)
            {
                break;
            }
        }

        return log[(prefixTrim + 2)..];
    }

    public override string ToString()
    {
        return string.IsNullOrEmpty(LogMessage)
            ? RawLog
            : LogMessage;
    }

    [GeneratedRegex(@"\[(.*?)\]\s*\[(.*?)\]\s*(.*)\s*([\s\S]*)")]
    private static partial Regex LogParseRegex();
}
