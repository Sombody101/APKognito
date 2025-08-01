using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using APKognito.Models;
using Microsoft.Extensions.Logging;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Brush = System.Windows.Media.Brush;
using LogEntryType = APKognito.Models.LogBoxEntry.LogEntryType;

namespace APKognito.Utilities.MVVM;

/// <summary>
/// Gives a horrible interface for logging to a <see cref="RichTextBox"/> while still adhering to <see cref="ObservableObject"/> rules.
/// </summary>
public partial class LoggableObservableObject : ViewModel, IViewable, IViewLogger
{
    [ObservableProperty]
    public partial ObservableCollection<LogBoxEntry> LogBoxEntries { get; set; } = [];

    /// <summary>
    /// The current <see cref="LoggableObservableObject"/>.
    /// </summary>
    public static LoggableObservableObject CurrentLoggableObject { get; private set; } = null!;

    public ISnackbarService? SnackbarService { get; private set; } = null!;

    /* Configs */
    protected bool LogIconPrefixes = true;
    protected bool DisableFileLogging = true;

    private string _indent = string.Empty;

    public void SetCurrentLogger()
    {
        CurrentLoggableObject = this;
    }

    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
#if RELEASE
        if (logLevel == Microsoft.Extensions.Logging.LogLevel.Debug)
        {
            return;
        }
#endif

        IEnumerable<object> scopes = LogScopeManager.GetCurrentScopeStates();

        LogLevel level = FileLogger.MicrosoftLogLevelToLocal(logLevel);
        LogEntryType entryType = LogBoxEntry.ConvertLogLevel(level);
        Brush color = FileLogger.LogLevelToBrush(level);

        string message = formatter(state, null);

        string scope = string.Join(' ', scopes);

        if (!string.IsNullOrWhiteSpace(scope))
        {
            message = $"{scope}: {message}";
        }

        if (!DisableFileLogging)
        {
            FileLogger.LogGeneric(message, level);
        }

        WriteGenericLog(message, color, entryType);

#if DEBUG
        if (exception is not null)
        {
            LogDebug(exception);
        }
#endif
    }

    public void WriteGenericLog(string text, [Optional] Brush? color, LogEntryType? logType = LogEntryType.None, bool newline = true)
    {
        WriteGenericLog(new StringBuilder(text), color, logType, newline);
    }

    public void WriteGenericLog(StringBuilder text, [Optional] Brush? color, LogEntryType? logType = LogEntryType.None, bool newline = false)
    {
        if (newline)
        {
            _ = text.AppendLine();
        }

        if (!string.IsNullOrEmpty(_indent))
        {
            _ = text.Insert(0, _indent);
        }

        LogBoxEntry newEntry = new()
        {
            Text = text.ToString(),
            Color = color,
            LogType = logType,
        };

        LogBoxEntries.Add(newEntry);
    }

    public void WriteGenericLogLine()
    {
        WriteGenericLogLine(string.Empty, null, null);
    }

    public void WriteGenericLogLine(string text, [Optional] Brush? color, LogEntryType? logType = LogEntryType.None)
    {
        WriteGenericLog(text, color, logType, newline: true);
    }

    public void WriteGenericLogLine(StringBuilder text, [Optional] Brush? color, LogEntryType? logType = LogEntryType.None)
    {
        WriteGenericLog(text, color, logType, newline: true);
    }

    public void Log(string log)
    {
        FileLogger.Log(log, DisableFileLogging);
        WriteGenericLogLine(log, logType: LogEntryType.Info);
    }

    public void LogInformation(string log)
    {
        FileLogger.Log(log, DisableFileLogging);
        WriteGenericLogLine(log, FileLogger.LogLevelColors.Info, logType: LogEntryType.Info);
    }

    public void LogSuccess(string log)
    {
        FileLogger.Log(log, DisableFileLogging);
        WriteGenericLogLine(log, Brushes.Lime, logType: LogEntryType.Success);
    }

    public void LogWarning(string log)
    {
        FileLogger.LogWarning(log, DisableFileLogging);
        WriteGenericLogLine(log, FileLogger.LogLevelColors.Warning, logType: LogEntryType.Warning);
    }

    public void LogError(string log)
    {
        FileLogger.LogError(log, DisableFileLogging);
        WriteGenericLogLine(log, FileLogger.LogLevelColors.Error, logType: LogEntryType.Error);
    }

    public void LogError(Exception ex)
    {
        FileLogger.LogException(ex, DisableFileLogging);
        WriteGenericLog(ex.Message, FileLogger.LogLevelColors.Error, logType: LogEntryType.Error);
        LogDebug(ex);
    }

    public void LogDebug(string log)
    {
        FileLogger.LogDebug(log);

#if DEBUG
        WriteGenericLogLine(log, FileLogger.LogLevelColors.Debug, logType: LogEntryType.Debug);
#endif
    }

    public void LogDebug(Exception ex)
    {
        FileLogger.LogDebug(ex);

#if DEBUG
        WriteGenericLog($"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", FileLogger.LogLevelColors.Debug, logType: LogEntryType.Debug);
#endif
    }

    public void AddIndent(char indenter = '\t')
    {
        _indent = $"{_indent}{indenter}";
    }

    public void AddIndentString(string indenter = "\t")
    {
        _indent = $"{_indent}{indenter}";
    }

    public void RemoveIndent()
    {
        _indent = _indent[..^1];
    }

    public void ResetIndent()
    {
        _indent = string.Empty;
    }

    public void ClearLogs()
    {
        LogBoxEntries.Clear();
    }

    public void DisplaySnack(string header, string body, ControlAppearance appearance, int displayTimeMs = 10_000)
    {
        if (SnackbarService is null)
        {
            throw new InvalidOperationException("No Snackpresenter was set.");
        }

        if (body.Length is 0)
        {
            body = header;
        }

        SymbolIcon icon = new()
        {
            Symbol = appearance switch
            {
                ControlAppearance.Info => SymbolRegular.Info24,
                ControlAppearance.Success => SymbolRegular.CheckmarkCircle24,
                ControlAppearance.Caution => SymbolRegular.Warning24,
                ControlAppearance.Danger => SymbolRegular.ErrorCircle24,
                _ => SymbolRegular.Empty
            },
        };

        SnackbarService.Show(header, body, appearance, icon, TimeSpan.FromMilliseconds(displayTimeMs));
    }

    public void SnackInfo(string header, string body)
    {
        DisplaySnack(header, body, ControlAppearance.Info);
    }

    public void SnackSuccess(string header, string body)
    {
        DisplaySnack(header, body, ControlAppearance.Success);
    }

    public void SnackWarning(string header, string body)
    {
        DisplaySnack(header, body, ControlAppearance.Caution);
    }

    public void SnackError(string header, string body)
    {
        DisplaySnack(header, body, ControlAppearance.Danger);
    }

    public void SnackError(string body)
    {
        DisplaySnack("Error", body, ControlAppearance.Danger);
    }

    protected void SetSnackbarProvider(ISnackbarService _snackbarService)
    {
        SnackbarService = _snackbarService;
    }

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        return true;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return new LogScope(state);
    }

    public static class LogScopeManager
    {
        private static readonly AsyncLocal<Stack<object>> _scopeStack = new();

        internal static void PushScope(object scope)
        {
            Stack<object>? stack = _scopeStack.Value;
            if (stack is null)
            {
                stack = new Stack<object>();
                _scopeStack.Value = stack;
            }

            stack.Push(scope);
        }

        internal static void PopScope()
        {
            Stack<object>? stack = _scopeStack.Value;
            if (stack is not null && stack.Count > 0)
            {
                _ = stack.Pop();

                if (stack.Count is 0)
                {
                    _scopeStack.Value = null!;
                }
            }
        }

        public static IEnumerable<object> GetCurrentScopeStates()
        {
            return _scopeStack.Value?.Reverse() ?? [];
        }
    }

    public sealed class LogScope : IDisposable
    {
        private bool _disposed;

        public LogScope(object state)
        {
            State = state;
            LogScopeManager.PushScope(this);
        }

        public object State { get; }

        public void Dispose()
        {
            if (!_disposed)
            {
                LogScopeManager.PopScope();
                _disposed = true;
            }
        }

        public override string? ToString()
        {
            return State.ToString();
        }
    }
}
