using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.ViewModels.Windows;
using APKognito.Views.Pages;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Documents;

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
/// Modified version of: https://github.com/VRPirates/rookie/blob/master/Utilities/Logger.cs
/// </summary>
public static class FileLogger
{
    public const string ReplacementUsername = "[:USER:]";

    private static readonly object lockObject = new();
    private static readonly string logFilePath = Path.Combine(App.AppData!.FullName, "applog.log");
    private static readonly string exceptionLogFilePath = Path.Combine(App.AppData!.FullName, "exlog.log");

    private static string UtcTime => DateTime.UtcNow.ToString("hh:mm:ss.fff tt: ");

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

        string newline = text.Length > 40 && text.Contains('\n')
            ? "\n\n"
            : "\n";

        string admin = MainWindowViewModel.LaunchedAsAdministrator
            ? "[ADMIN]"
            : string.Empty;

        string logEntry = $"[{UtcTime}{logLevel.ToString().ToUpper()}] {admin}\t[{GetCallerInfo()}] {text}{newline}";
        LogGenericFinal(logEntry);
    }

    public static void LogGenericException(Exception ex, string partnerLog = "")
    {
        string json = JsonConvert.SerializeObject(ex, Formatting.Indented);

        StringBuilder log = new();

        log.Append('[').Append(UtcTime).Append("]: EXCEPTION");

        if (MainWindowViewModel.LaunchedAsAdministrator)
        {
            log.Append(" [ADMIN]");
        }

        log.Append(string.IsNullOrWhiteSpace(partnerLog) ? "[No log]" : string.Empty).Append(": ")
            .AppendLine(ex.GetType().Name)
            .AppendLine(json).AppendLine()
            .AppendLine("Formatted Trace:")
            .AppendLine(ex.StackTrace).AppendLine()
            .AppendLine("-- END LOG --")
            .AppendLine();

        LogGenericFinal(log.ToString(), ex);
    }

    public static void Log(string log)
    {
        LogGeneric(log, LogLevel.INFO);
    }

    public static void LogWarning(string log)
    {
        LogGeneric(log, LogLevel.WARNING);
    }

    public static void LogError(string log)
    {
        LogGeneric(log, LogLevel.ERROR);
    }

    public static void LogFatal(string log)
    {
        LogGeneric(log, LogLevel.FATAL);
    }

    public static void LogFatal(Exception exception)
    {
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

    public static void LogException(Exception exception)
    {
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
            ConfigurationFactory.GetConfigInfo<RenameSessionList>().GetCompletePath()
        ];

        string packPath = Path.Combine(App.AppData!.FullName, "logpack");
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

        File.Create(Path.Combine(packPath, App.Version.GetVersion())).Close();

        string outputPack = Path.Combine(App.AppData.FullName, "logpack.zip");

        // Delete old logpack
        if (File.Exists(outputPack))
        {
            File.Delete(outputPack);
        }

        ZipFile.CreateFromDirectory(packPath, outputPack);

        Directory.Delete(packPath, true);

        return outputPack;
    }

    private static void LogGenericFinal(string entry, Exception? ex = null)
    {
        try
        {
            entry = entry.Redact();

            lock (lockObject)
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
        StackTrace stackTrace = new(true);

        if (stackTrace.FrameCount >= 3)
        {
            int frameDepth = 2;

        Retry:
            StackFrame frame = stackTrace.GetFrame(frameDepth)!;
            MethodBase? method = frame!.GetMethod();

            // Prevents Log aliases from being selected as the caller
            if (method?.DeclaringType == typeof(FileLogger) || method?.DeclaringType == typeof(LoggableObservableObject))
            {
                ++frameDepth;
                goto Retry;
            }

            string className;
            string methodName;

            if (typeof(IAsyncStateMachine).IsAssignableFrom(method?.DeclaringType))
            {
                // Fix async methods (<AwaitedMethodName>d__10.MoveNext -> RealClassName.AwaitedMethodName)
                className = method.DeclaringType.DeclaringType?.Name ?? "[Unknown]";
                methodName = method.DeclaringType.Name.TrimStart('<');
                methodName = methodName[0..methodName.IndexOf('>')];
            }
            else
            {
                className = method?.DeclaringType?.Name ?? "[Unknown]";
                methodName = method?.Name ?? "[Unknown]";
            }

            return $"{className}.{methodName}";
        }

        return $"[Failed to get caller info: ";
    }
}