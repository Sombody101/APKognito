using System.Globalization;
using System.Text.RegularExpressions;
using APKognito.Utilities;
using Brush = System.Windows.Media.Brush;

namespace APKognito.Models;

public partial record LogViewerLine
{
    private const string DEFAULT = "[None]";

    public string RawLog { get; }
    public bool HasException { get; }

    public string LogTime { get; private set; } = string.Empty;

    public string CallSite { get; private set; } = DEFAULT;

    public string LogMessage { get; private set; } = DEFAULT;

    public LogLevel LogLevel { get; private set; }

    public string? ExceptionLog { get; private set; }

    public bool IsAdmin { get; private set; }

    public Visibility ExceptionLogVisible => string.IsNullOrEmpty(ExceptionLog)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Brush Background => FileLogger.LogLevelToBrush(LogLevel);

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

        if (match.Groups[2].Success)
        {
            IsAdmin = true;
        }

        string timeString = match.Groups[1].Value;
        int lastSpace = timeString[..timeString.LastIndexOf(' ')].LastIndexOf(' ') + 1;

        string rawLogLevel = timeString[lastSpace..];

        _ = Enum.TryParse(rawLogLevel, out LogLevel level);
        LogLevel = level;
        timeString = timeString[..lastSpace];
        CallSite = match.Groups[3].Value.Replace(".", " → ");
        LogMessage = match.Groups[4].Value;

        if (HasException)
        {
            ExceptionLog = ParseExceptionLog(match.Groups[5].Value);
        }

        const string TIME_FORMAT = "hh:mm:ss.fff";
        string trimmedTime = timeString.Trim();
        if (DateTime.TryParseExact(trimmedTime, TIME_FORMAT, CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal, out DateTime dateTime))
        {
            LogTime = dateTime.ToString("hh:mm:ss tt");
        }
        else
        {
            int splitIndex = trimmedTime.IndexOf(' ');
            LogTime = splitIndex is not -1
                ? trimmedTime[splitIndex..]
                : trimmedTime;
        }
    }

    public bool Contains(string key, StringComparison comparison)
    {
        return RawLog.Contains(key, comparison)
            || (HasException && ExceptionLog!.Contains(key, comparison));
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

        prefixTrim += 2;

        return prefixTrim > log.Length
            ? throw new ArgumentException("Invalid exception format.")
            : log[prefixTrim..];
    }

    public override string ToString()
    {
        return string.IsNullOrEmpty(LogMessage)
            ? RawLog
            : LogMessage;
    }

    [GeneratedRegex(@"\[(.*?)( ADMIN)?\]\s*\[(.*?)\]\s*(.*)\s*([\s\S]*)")]
    private static partial Regex LogParseRegex();
}
