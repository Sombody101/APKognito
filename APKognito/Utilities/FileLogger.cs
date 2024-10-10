using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;

namespace APKognito.Utilities;

public enum LogLevel
{
    INFO,
    WARNING,
    ERROR,
    FATAL,
    DEBUG,
    TRACE,
}

/// <summary>
/// https://github.com/VRPirates/rookie/blob/master/Utilities/Logger.cs
/// </summary>
public static class FileLogger
{
    private static readonly object lockObject = new object();
    private static readonly string sensitiveName = Environment.UserName;
    private static string? logFilePath;

    public static bool LogGeneric(string text, LogLevel logLevel = LogLevel.INFO, bool ret = true)
    {
        logFilePath ??= Path.Combine(App.AppData!.FullName, "applog.log");

        if (string.IsNullOrWhiteSpace(text) || text.Length <= 5)
        {
            return ret;
        }

        string time = DateTime.UtcNow.ToString("hh:mm:ss.fff tt (UTC): ");

        string newline = text.Length > 40 && text.Contains('\n')
            ? "\n\n"
            : "\n";

        string logEntry = $"[{time}{logLevel.ToString().ToUpper()}]\t[{GetCallerInfo()}] {text.Replace(sensitiveName, "[:USER:]")}{newline}";

        try
        {
            lock (lockObject)
            {
                File.AppendAllText(logFilePath, logEntry);
            }
        }
        catch
        {
            // Exception
        }

        return ret;
    }

    public static bool Log(string log, bool ret = false)
    {
        return LogGeneric(log, LogLevel.INFO, ret);
    }

    public static bool LogWarning(string log, bool ret = false)
    {
        return LogGeneric(log, LogLevel.WARNING, ret);
    }

    public static bool LogError(string log, bool ret = false)
    {
        return LogGeneric(log, LogLevel.ERROR, ret);
    }

    public static bool LogFatal(string log, bool ret = false)
    {
        return LogGeneric(log, LogLevel.FATAL, ret);
    }

    public static bool LogFatal(Exception exception, bool ret = false)
    {
        return LogGeneric(JsonConvert.SerializeObject(exception.InnerException ?? exception), LogLevel.FATAL, ret);
    }

    public static bool LogDebug(string log, bool ret = false)
    {
        return LogGeneric(log, LogLevel.DEBUG, ret);
    }

    public static bool LogException(Exception exception, bool ret = false)
    {
        return LogGeneric(JsonConvert.SerializeObject(exception.InnerException ?? exception), LogLevel.TRACE, ret);
    }

    private static string GetCallerInfo()
    {
        System.Diagnostics.StackTrace stackTrace = new(true);

       if (stackTrace.FrameCount >= 3)
        {
            int frameDepth = 2;

            Retry:
            StackFrame frame = stackTrace.GetFrame(frameDepth)!;
            MethodBase? method = frame!.GetMethod();
            
            // Prevents Log aliases from being selected as the caller
            if (method?.DeclaringType == typeof(FileLogger))
            {
                ++frameDepth;
                goto Retry;
            }

            string className = method?.DeclaringType?.Name ?? "[Unknown]";
            string methodName = method?.Name ?? "[Unknown]";
            string callerInfo = $"{className}.{methodName}";
            return callerInfo;
        }

        return string.Empty;
    }
}