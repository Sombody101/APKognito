using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Windows;
using APKognito.Views.Pages;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Documents;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace APKognito.Utilities;

public enum LogLevel
{
    ANY = int.MaxValue,

    INFO = 0,
    WARNING,
    ERROR,
    FATAL,
    DEBUG,
    TRACE,
}

/// <summary>
/// Modified version of: https://github.com/VRPirates/rookie/blob/master/Utilities/Logger.cs
/// </summary>
public static class FileLogger
{
    public const string TimeFormatString = "hh:mm:ss.fff tt:";
    public const string ReplacementUsername = "[:USER:]";

    private static readonly object _lock = new();
    private static readonly string logFilePath = Path.Combine(App.AppDataDirectory!.FullName, "applog.log");
    private static readonly string exceptionLogFilePath = Path.Combine(App.AppDataDirectory!.FullName, "exlog.log");

    private static string UtcTime => DateTime.UtcNow.ToString(TimeFormatString, CultureInfo.InvariantCulture);

    static FileLogger()
    {
        try
        {
            FileInfo logFile = new(logFilePath);
            if (logFile.Length >= (1024 * 1024 * 4)) // 4MB
            {
                logFile.Delete();
            }
        }
        catch
        {
            // Probably doesn't exist yet
        }
    }

    public static void LogGeneric(string text, LogLevel logLevel = LogLevel.INFO)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= 5)
        {
            return;
        }

        text = text.Trim('\n');

        string newline = text.Length > 40 && text.Contains('\n')
            ? "\n\n"
            : "\n";

        string admin = MainWindowViewModel.LaunchedAsAdministrator
            ? " ADMIN"
            : string.Empty;

        string logEntry = $"[{UtcTime} {logLevel.ToString().ToUpper()}{admin}]    [{GetCallerInfo()}] {text}{newline}";
        LogGenericFinal(logEntry);
    }

    public static void LogGenericException(Exception ex, string partnerLog = "")
    {
        StringBuilder log = new();

        log.Append('[').Append(UtcTime).Append("]: EXCEPTION");

        if (MainWindowViewModel.LaunchedAsAdministrator)
        {
            log.Append(" [ADMIN]");
        }

        partnerLog = partnerLog.Trim('\n');

        log.Append(string.IsNullOrWhiteSpace(partnerLog) ? "[No log]" : string.Empty).Append(": ")
            .AppendLine(GetFormattedException(ex)).AppendLine()
            .AppendLine("-- END LOG --")
            .AppendLine();

        LogGenericFinal(log.ToString(), ex);
    }

    public static void Log(string log, bool ignore = false)
    {
        if (ignore)
        {
            return;
        }

        LogGeneric(log, LogLevel.INFO);
    }

    public static void LogWarning(string log, bool ignore = false)
    {
        if (ignore)
        {
            return;
        }

        LogGeneric(log, LogLevel.WARNING);
    }

    public static void LogError(string log, bool ignore = false)
    {
        if (ignore)
        {
            return;
        }

        LogGeneric(log, LogLevel.ERROR);
    }

    public static void LogFatal(string log)
    {
        LogGeneric(log, LogLevel.FATAL);
    }

    public static void LogFatal(Exception exception, bool ignore = false)
    {
        if (ignore)
        {
            return;
        }

        LogGeneric($"StackTrace added to exlog: {exception.GetType().Name}: {exception.Message}", LogLevel.FATAL);
        LogGenericException(exception, "[LogFatal added message: Fatal exception]");
    }

    public static void LogDebug(string log)
    {
        LogGeneric(log, LogLevel.DEBUG);
    }

    public static void LogDebug(Exception exception)
    {
        LogGenericException(exception);
    }

    public static void LogException(Exception exception, bool ignore = false)
    {
        if (ignore)
        {
            return;
        }

        LogGenericException(exception);
    }

    public static void LogException(string log, Exception exception)
    {
        LogGenericException(exception, log);
    }

    public static string CreateLogpack()
    {
        string[] filesToPack = [
            logFilePath,
            exceptionLogFilePath,
            Path.Combine(ConfigurationFactory.Instance.ConfigurationDirectory, ConfigurationFactory.Instance.GetConfigInfo<RenameSessionList>().FileName)
        ];

        string packPath = Path.Combine(App.AppDataDirectory!.FullName, "logpack");
        _ = Directory.CreateDirectory(packPath);

        StringBuilder errorFiles = new();
        foreach (var file in filesToPack)
        {
            if (File.Exists(file))
            {
                File.Copy(file, Path.Combine(packPath, Path.GetFileName(file)), true);
            }
            else
            {
                errorFiles.AppendLine($"Failed to locate file: {file}");
            }
        }

        if (errorFiles.Length > 0)
        {
            File.WriteAllText(Path.Combine(packPath, "unpacked.txt"), errorFiles.ToString());
        }

        // Items that need to be packed manually
        string logBoxPath = Path.Combine(packPath, "logbox.txt");
        var hmv = HomePage.Instance;

        if (hmv is null)
        {
            File.WriteAllText(logBoxPath, "[Null]");
        }
        else
        {
            var lines = ((Paragraph)hmv.APKLogs.Document.Blocks.LastBlock).Inlines
                .Select(line => line.ContentStart.GetTextInRun(LogicalDirection.Forward));

            File.WriteAllText(logBoxPath, string.Join("\r\n", lines));
        }

        File.Create(Path.Combine(packPath, App.Version.GetFullVersion())).Close();

        string outputPack = Path.Combine(App.AppDataDirectory.FullName, "logpack.zip");

        // Delete old logpack
        if (File.Exists(outputPack))
        {
            File.Delete(outputPack);
        }

        ZipFile.CreateFromDirectory(packPath, outputPack);

        Directory.Delete(packPath, true);

        return outputPack;
    }

    public static Brush LogLevelToBrush(LogLevel level)
    {
        return level switch
        {
            LogLevel.INFO       => LogLevelColors.Info,
            LogLevel.WARNING    => LogLevelColors.Warning,
            LogLevel.ERROR      => LogLevelColors.Error,
            LogLevel.FATAL      => LogLevelColors.Fatal,
            LogLevel.DEBUG      => Brushes.Cyan,
            LogLevel.TRACE      => LogLevelColors.Trace,

            _ => throw new ArgumentException($"No color mapping made for log level '{level}'")
        };
    }

    private static void LogGenericFinal(string entry, Exception? ex = null)
    {
        try
        {
            entry = entry.Redact();

            lock (_lock)
            {
                if (ex is null)
                {
                    File.AppendAllText(logFilePath, entry);
                }
                else
                {
                    File.AppendAllText(logFilePath, $"Exception: {ex?.GetType().Name ?? "[NULL]"} appended to exception logs.\r\n");
                    File.AppendAllText(exceptionLogFilePath, entry);
                }
            }
        }
        catch
        {
            // Exception
        }
    }

    private static string GetCallerInfo()
    {
        const int initialFrameDepth = 2;
        const int maxFrames = 10;

        Type fileLoggerType = typeof(FileLogger);
        Type loggableObservableObjectType = typeof(LoggableObservableObject);

        StackTrace stackTrace = new();

        for (int frameDepth = initialFrameDepth; frameDepth < stackTrace.FrameCount
            && frameDepth < initialFrameDepth + maxFrames; frameDepth++)
        {
            StackFrame? frame = stackTrace.GetFrame(frameDepth);
            MethodBase? method = frame?.GetMethod();

            if (method is null
                || method.DeclaringType is null
                || method.DeclaringType == fileLoggerType
                || method.DeclaringType == loggableObservableObjectType)
            {
                continue;
            }

            string className;
            string methodName;

            if (typeof(IAsyncStateMachine).IsAssignableFrom(method.DeclaringType))
            {
                // Fix async methods (<AwaitedMethodName>d__10.MoveNext -> RealClassName.AwaitedMethodName)
                className = method.DeclaringType.DeclaringType?.Name ?? "[Unknown.IAsyncClass]";
                methodName = method.DeclaringType.Name.TrimStart('<');
                methodName = methodName[0..methodName.IndexOf('>')];
            }
            else
            {
                className = method.DeclaringType.Name ?? "[Unknown.Class]";
                methodName = method.Name ?? "[Unknown.Method]";
            }

            return $"{className}.{methodName}";
        }

        return "Failed to get caller info";
    }

    private static string GetFormattedException(Exception ex)
    {
        StringBuilder exception = new();

        exception.Append(ex.GetType().Name).Append(": ").AppendLine(ex.Message)
            .AppendLine(ex.StackTrace ?? "[NO STACK]");

        return exception.ToString();
    }

    public static class LogLevelColors
    {
        static LogLevelColors()
        {
            PopulateColors(ApplicationThemeManager.GetAppTheme() is ApplicationTheme.Dark);

            ApplicationThemeManager.Changed += (sender, e) =>
            {
                PopulateColors(sender is ApplicationTheme.Dark);
            };
        }

        public static Brush Info { get; private set; }
        public static Brush Warning { get; private set; }
        public static Brush Error { get; private set; }
        public static Brush Fatal { get; private set; }
        public static Brush Debug { get; private set; }
        public static Brush Trace { get; private set; }

        public static SolidColorBrush ToBrush(string value)
        {
            return (SolidColorBrush)new BrushConverter().ConvertFromString(value)!;
        }

        private static void PopulateColors(bool dark = true)
        {
            Error = ToBrush("#F44336");

            if (dark)
            {
                Info = ToBrush("#006d9e");
                Warning = ToBrush("#FF9800");
                Fatal = ToBrush("#FF5722");
                Debug = ToBrush("#009688");
                Trace = ToBrush("#3F51B5");
                return;
            }

            Info = ToBrush("#5BB6FF");
            Warning = ToBrush("#FF9800");
            Fatal = ToBrush("#FF7043");
            Debug = ToBrush("#009688");
            Trace = ToBrush("#3F51B5");
        }
    }
}