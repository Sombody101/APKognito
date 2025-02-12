using APKognito.Utilities;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace APKognito.Models;

public class LogViewerLine
{
    private const string DEFAULT = "[None]";

    public string RawLog { get; }
    public bool IsException { get; }

    public DateTime LogTime { get; private set; }
    public string CallSite { get; private set; } = DEFAULT;
    public string LogMessage { get; private set; } = DEFAULT;
    public string LogLevel { get; private set; } = DEFAULT;

    public LogViewerLine(string log, bool exceptionLog)
    {
        RawLog = log;
        IsException = exceptionLog;

        ParseLog();
    }

    private void ParseLog()
    {
        if (string.IsNullOrEmpty(RawLog) || RawLog[0] != '[')
        {
            throw new ArgumentException("Invalid or empty raw log");
        }

        string pattern = @"\[(.*?)\]\s*\[(.*?)\]\s*(.*)";
        Match match = Regex.Match(RawLog, pattern);

        if (!match.Success)
        {
            throw new FormatException($"Log format does not match expected pattern: {RawLog}");
        }

        string timeString = match.Groups[1].Value;
        int lastSpace = timeString.LastIndexOf(' ');
        timeString = timeString[..(lastSpace + 1)];

        LogLevel = timeString[lastSpace..];
        CallSite = match.Groups[2].Value;
        LogMessage = match.Groups[3].Value;
        LogTime = DateTime.ParseExact(timeString, FileLogger.TimeFormatString, CultureInfo.InvariantCulture);
    }

    public override string ToString()
    {
        return RawLog;
    }
}
