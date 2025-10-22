using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Documents;
using System.Windows.Media;
using APKognito.AdbTools;
using APKognito.Cli;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.ConsoleAbstractions;
using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Windows;
using APKognito.Views.Pages;
using Microsoft.Extensions.Logging;
using Wpf.Ui.Appearance;

#if DEBUG
using Color = System.Windows.Media.Color;
using Paragraph = System.Windows.Documents.Paragraph;
using Spectre.Console;
#endif

namespace APKognito.Utilities;

public enum LogLevel
{
    ANY = int.MaxValue,

    TRACE = 0,
    DEBUG,
    INFO,
    WARNING,
    ERROR,
    FATAL,
    NONE,
}

/// <summary>
/// Modified version of: https://github.com/VRPirates/rookie/blob/master/Utilities/Logger.cs
/// </summary>
public static class FileLogger
{
    public const string TIME_FORMAT_STRING = "hh:mm:ss.fff tt:";
    public const string USER_REPLACEMENT_STRING = "[:USER:]";

    private static readonly Lock s_lock = new();
    private static readonly string s_logFilePath = Path.Combine(App.AppDataDirectory!.FullName, "applog.log");
    private static readonly string s_exceptionLogFilePath = Path.Combine(App.AppDataDirectory!.FullName, "exlog.log");

    private static string UtcFormattedTime => DateTime.UtcNow.ToString(TIME_FORMAT_STRING, CultureInfo.InvariantCulture);

    static FileLogger()
    {
        const int ONE_MB = 1024 * 1024;

        try
        {
            FileInfo logFile = new(s_logFilePath);
            if (logFile.Length >= (ONE_MB * 4) && !TrimLogFile(logFile.FullName, ONE_MB * 2))
            {
                logFile.Delete();
            }
        }
        catch
        {
            // Probably doesn't exist yet
        }

#if DEBUG
        Log("User information redaction disabled on debug build. Switch to a public debug or public release to have user information redacted.");
#endif
    }

    public static void LogGeneric(string text, LogLevel logLevel = LogLevel.INFO)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= 5)
        {
            return;
        }

        text = text.Trim('\n');

        string lineSuffix = text.Length > 40 && text.Contains('\n')
            ? "\n\n"
            : "\n";

#if DEBUG
        StringBuilder builder = new();

        builder.Append("[[")
            .Append(DateTime.Now.ToString(TIME_FORMAT_STRING)).Append(' ')
            .Append(LogLevelColors.GetAnsiColor(logLevel)).Append(logLevel.ToString().ToUpper()).Append("[/] ")
            .Append(MainWindowViewModel.LaunchedAsAdministrator ? " [yellow]ADMIN[/]" : string.Empty)
            .Append("[[").Append(GetCallerInfo().EscapeMarkup()).Append("]] ").Append(text).Append(lineSuffix);

        string logEntry = builder.ToString();
#else
        string logEntry = $"[{UtcFormattedTime} {logLevel.ToString().ToUpper()}" +
                          $"{(MainWindowViewModel.LaunchedAsAdministrator ? " ADMIN" : string.Empty)}]" +
                          $" [{GetCallerInfo()}] {text}{lineSuffix}";
#endif

        LogGenericFinal(logEntry);
    }

    public static void LogGenericException(Exception ex, string partnerLog = "")
    {
        StringBuilder log = new();

        _ = log.Append('[').Append(UtcFormattedTime).Append("]: EXCEPTION");

        if (MainWindowViewModel.LaunchedAsAdministrator)
        {
            _ = log.Append(" [ADMIN]");
        }

        partnerLog = partnerLog.Trim('\n');

        _ = log.Append(string.IsNullOrWhiteSpace(partnerLog) ? "[No log]" : string.Empty).Append(": ")
            .AppendLine(GetFormattedException(ex)).AppendLine()
            .AppendLine("-- END LOG --")
            .AppendLine();

#if DEBUG
        LogGenericFinal(log.ToString().EscapeMarkup(), ex);
#else
        LogGenericFinal(log.ToString(), ex);
#endif
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

    public static void LogFatal(string message, Exception ex, bool ignore = false)
    {
        if (ignore)
        {
            return;
        }

        LogGenericException(ex, message);
    }

    [Conditional("DEBUG")]
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

    public static async Task<string> CreateLogpackAsync(bool includeAndroidCrash)
    {
        ConfigurationFactory configFactory = App.GetService<ConfigurationFactory>()!;

        ArgumentNullException.ThrowIfNull(configFactory);

        string[] filesToPack = [
            s_logFilePath,
            s_exceptionLogFilePath,
            Path.Combine(configFactory.ConfigurationDirectory, configFactory.GetConfigInfo<RenameSessionList>().FileName)
        ];

        string packPath = Path.Combine(App.AppDataDirectory!.FullName, "logpack");
        _ = Directory.CreateDirectory(packPath);

        if (includeAndroidCrash)
        {
            // Get android log first
            AdbCommandOutput crashLogs = await AdbManager.QuickCommandAsync("logcat -b crash -d", true);

            using FileStream file = File.OpenWrite(Path.Combine(packPath, "package-crash.log"));
            using StreamWriter writer = new(file);

            await writer.WriteLineAsync("-- START STDOUT");
            await writer.WriteLineAsync(crashLogs.StdOut.AsMemory());
            await writer.WriteLineAsync("-- END STDOUT");
            await writer.WriteLineAsync("-- START STDERR");
            await writer.WriteLineAsync(crashLogs.StdErr.AsMemory());
            await writer.WriteLineAsync("-- END STDERR");

            await writer.FlushAsync();
            await file.FlushAsync();
        }

        StringBuilder errorFiles = new();
        foreach (string file in filesToPack)
        {
            if (File.Exists(file))
            {
                File.Copy(file, Path.Combine(packPath, Path.GetFileName(file)), true);
            }
            else
            {
                _ = errorFiles.AppendLine($"Failed to locate file: {file}");
            }
        }

        if (errorFiles.Length > 0)
        {
            await File.WriteAllTextAsync(Path.Combine(packPath, "unpacked.txt"), errorFiles.ToString());
        }

        // Items that need to be packed manually
        string logBoxPath = Path.Combine(packPath, "logbox.txt");
        HomePage? homePage = App.GetService<HomePage>();

        if (homePage is null)
        {
            await File.WriteAllTextAsync(logBoxPath, "[Null]");
        }
        else
        {
            IEnumerable<string> lines = homePage.APKLogs.Document.Blocks
                .Where(b => b is Paragraph)
                .SelectMany(p => ((Paragraph)p).Inlines
                    .Select(line => line.ContentEnd.GetTextInRun(LogicalDirection.Forward)));

            await File.WriteAllTextAsync(logBoxPath, string.Join("\r\n", lines));
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
            LogLevel.INFO => LogLevelColors.Info,
            LogLevel.WARNING => LogLevelColors.Warning,
            LogLevel.ERROR => LogLevelColors.Error,
            LogLevel.FATAL => LogLevelColors.Fatal,
            LogLevel.DEBUG => Brushes.Cyan,
            LogLevel.TRACE => LogLevelColors.Trace,

            _ => throw new ArgumentException($"No color mapping made for log level '{level}'")
        };
    }

    public static LogLevel MicrosoftLogLevelToLocal(Microsoft.Extensions.Logging.LogLevel level)
    {
        return level switch
        {
            Microsoft.Extensions.Logging.LogLevel.Trace => LogLevel.TRACE,
            Microsoft.Extensions.Logging.LogLevel.Debug => LogLevel.DEBUG,
            Microsoft.Extensions.Logging.LogLevel.Information => LogLevel.INFO,
            Microsoft.Extensions.Logging.LogLevel.Warning => LogLevel.WARNING,
            Microsoft.Extensions.Logging.LogLevel.Error => LogLevel.ERROR,
            Microsoft.Extensions.Logging.LogLevel.Critical => LogLevel.FATAL,
            _ => LogLevel.NONE,
        };
    }

    private static void LogGenericFinal(string entry, Exception? ex = null)
    {
        try
        {
#if RELEASE
            entry = entry.Redact();
#endif

            if (CliMain.ConsoleActive)
            {
                ConsoleAbstraction.WriteLine(entry.TrimEnd());

                if (ex is not null)
                {
                    ConsoleAbstraction.WriteException(ex);
                }
            }

#if DEBUG
            entry = ConsoleAbstraction.RemoveMarkup(entry);
#endif

            lock (s_lock)
            {
                if (ex is null)
                {
                    File.AppendAllText(s_logFilePath, entry);
                }
                else
                {
                    File.AppendAllText(s_logFilePath, $"Exception: {ex?.GetType().Name ?? "[NULL]"} appended to exception logs.\r\n");
                    File.AppendAllText(s_exceptionLogFilePath, entry);
                }
            }
        }
        catch (Exception fex)
        {
#if DEBUG
            AnsiConsole.MarkupLine("[red][[FAILED TO LOG EXCEPTION]][/]");
            AnsiConsole.WriteException(fex);

            if (ex is not null)
            {
                AnsiConsole.WriteLine("Original exception:");
                AnsiConsole.WriteException(ex);
            }
#endif

            // Exception
        }
    }

    private static string GetCallerInfo()
    {
        const int initialFrameDepth = 2;
        const int maxFrames = 10;

        Type fileLoggerType = typeof(FileLogger);
        Type loggableObservableObjectType = typeof(LoggableObservableObject);
        Type loggerExtensionsType = typeof(LoggerExtensions);

        StackTrace stackTrace = new();

        for (int frameDepth = initialFrameDepth; frameDepth < stackTrace.FrameCount
            && frameDepth < initialFrameDepth + maxFrames; frameDepth++)
        {
            StackFrame? frame = stackTrace.GetFrame(frameDepth);
            MethodBase? method = frame?.GetMethod();

            if (method is null or { DeclaringType: null }
                || method.DeclaringType == fileLoggerType
                || method.DeclaringType == loggableObservableObjectType
                || method.DeclaringType == loggerExtensionsType)
            {
                continue;
            }

            string className;
            string methodName;

            if (typeof(IAsyncStateMachine).IsAssignableFrom(method.DeclaringType))
            {
                // Fix async methods (<AwaitedMethodName>d__10.MoveNext -> RealClassName.AwaitedMethodName)
                className = GetNonCompilerGeneratedClassName(method);
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

        static string GetNonCompilerGeneratedClassName(MethodBase method)
        {
            Type? declaringType = method.DeclaringType;
            while (declaringType?.Name.StartsWith('<') is true)
            {
                declaringType = declaringType.DeclaringType;
            }

            return declaringType?.Name ?? "[nobase]";
        }
    }

    private static string GetFormattedException(Exception ex)
    {
        StringBuilder exception = new();

        _ = exception.Append(ex.GetType().Name).Append(": ").AppendLine(ex.Message)
            .AppendLine(ex.StackTrace ?? "[NO STACK]");

        return exception.ToString();
    }

    private static bool TrimLogFile(string filePath, int lineCount)
    {
        string tempFilePath = Path.GetRandomFileName();

        try
        {
            using (StreamReader reader = new(filePath))
            using (StreamWriter writer = new(tempFilePath))
            {
                for (int i = 0; i < lineCount; i++)
                {
                    if (reader.ReadLine() is null)
                    {
                        Console.WriteLine($"Warning: File has fewer than {lineCount} lines. All lines will be removed.");
                        break;
                    }
                }

                string? line;
                while ((line = reader.ReadLine()) is not null)
                {
                    writer.WriteLine(line);
                }
            }

            File.Delete(filePath);
            File.Move(tempFilePath, filePath);

            return true;
        }
        catch (Exception ex)
        {
            LogError(ex.Message);

            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }

        return false;
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

        public static Brush Info { get; private set; } = null!;
        public static Brush Warning { get; private set; } = null!;
        public static Brush Error { get; private set; } = null!;
        public static Brush Fatal { get; private set; } = null!;
        public static Brush Debug { get; private set; } = null!;
        public static Brush Trace { get; private set; } = null!;

        public static SolidColorBrush ToBrush(int color)
        {
            byte red = (byte)((color >> 16) & 0xFF);
            byte green = (byte)((color >> 8) & 0xFF);
            byte blue = (byte)(color & 0xFF);

            var newColor = new SolidColorBrush(Color.FromArgb(255, red, green, blue));
            newColor.Freeze();
            return newColor;
        }

        private static void PopulateColors(bool dark = true)
        {
            Error = ToBrush(0xF44336);

            if (dark)
            {
                LoadDarkColors();
                return;
            }

            LoadLightColors();
        }

        private static void LoadDarkColors()
        {
            Info = ToBrush(0x1cb1f5);
            Warning = ToBrush(0xFF9800);
            Fatal = ToBrush(0xFF5722);
            Debug = ToBrush(0x00ffff);
            Trace = ToBrush(0x3F51B5);
        }

        private static void LoadLightColors()
        {
            Info = ToBrush(0x5BB6FF);
            Warning = ToBrush(0xFF9800);
            Fatal = ToBrush(0xFF7043);
            Debug = ToBrush(0x00ffff);
            Trace = ToBrush(0x3F51B5);
        }

#if DEBUG
        internal static string GetAnsiColor(LogLevel level)
        {
            return level switch
            {
                LogLevel.TRACE => "[cyan1]",
                LogLevel.DEBUG => "[cyan1]",
                LogLevel.INFO => "[dodgerblue2]",
                LogLevel.WARNING => "[yellow]",
                LogLevel.ERROR => "[red]",
                LogLevel.FATAL => "[deeppink2]",
                _ => string.Empty
            };
        }
#endif
    }
}
