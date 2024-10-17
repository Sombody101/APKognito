using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Views.Pages;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Controls;
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
    public const string ReplacmentUsername = "[:USER:]";

    private static readonly object lockObject = new object();
    private static string logFilePath = Path.Combine(App.AppData!.FullName, "applog.log");

    public static void LogGeneric(string text, LogLevel logLevel = LogLevel.INFO)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= 5)
        {
            return;
        }

        string time = DateTime.UtcNow.ToString("hh:mm:ss.fff tt: ");

        string newline = text.Length > 40 && text.Contains('\n')
            ? "\n\n"
            : "\n";

        string logEntry = $"[{time}{logLevel.ToString().ToUpper()}]\t[{GetCallerInfo()}] {text.Redact()}{newline}";

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
        LogGeneric(JsonConvert.SerializeObject(exception.InnerException ?? exception), LogLevel.FATAL);
    }

    public static void LogDebug(string log)
    {
        LogGeneric(log, LogLevel.DEBUG);
    }

    public static void LogException(Exception exception)
    {
        LogGeneric(JsonConvert.SerializeObject(exception.InnerException ?? exception), LogLevel.TRACE);
    }

    public static void LogException(string log, Exception exception)
    {
        LogGeneric($"{log}: {JsonConvert.SerializeObject(exception.InnerException ?? exception)}", LogLevel.TRACE);
    }

    public static string CreateLogpack()
    {
        string[] filesToPack = [
            logFilePath,
            ConfigurationFactory.GetConfigInfo<RenameSessionList>().CompletePath()
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
                errorFiles.AppendLine($"Failed to locate file: {file.Redact()}");
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
                .Select(line => line.ContentStart.GetTextInRun(LogicalDirection.Forward).Redact());

            File.WriteAllText(logBoxPath, string.Join("\r\n", lines));
        }

        File.Create(Path.Combine(packPath, App.GetVersion())).Close();

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

            string className;
            string methodName;

            if (typeof(IAsyncStateMachine).IsAssignableFrom(method?.DeclaringType))
            {
                // Fix async methods (<AwaitedMethodName>d__10.MoveNext -> RealClassName.AwaitedMethodName)
                className = method.DeclaringType.DeclaringType?.Name ?? "[Unknown]";
                methodName = method.DeclaringType.Name.TrimStart('<');
                methodName = methodName[0..(methodName.IndexOf('>'))];
            }
            else
            {
                className = method?.DeclaringType?.Name ?? "[Unknown]";
                methodName = method?.Name ?? "[Unknown]";
            }

            return $"{className}.{methodName}";
        }

        return string.Empty;
    }
}