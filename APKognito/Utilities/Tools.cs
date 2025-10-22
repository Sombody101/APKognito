using System.Diagnostics;
using System.Runtime.CompilerServices;
using APKognito.ViewModels.Windows;
using Humanizer;
using Wpf.Ui.Controls;

#if DEBUG
using Spectre.Console;
using Microsoft.Extensions.Logging;
using APKognito.ConsoleAbstractions;
using System.Runtime.InteropServices;
using static APKognito.Utilities.MVVM.LoggableObservableObject;
#endif

namespace APKognito.Utilities;

internal static class Tools
{
    private const string DEFAULT_MS_STRING_FORMAT = "n0";

    public static async Task AdminExitCheckAsync()
    {
        if (!MainWindowViewModel.LaunchedAsAdministrator)
        {
            return;
        }

        // Give the window roughly a millisecond to render
        await Task.Delay(1);

        MessageBoxResult result = await new MessageBox()
        {
            Title = "Launched as Admin!",
            Content = "It's not recommended to launch an application as admin, especially one that interacts with your drive(s)! " +
                "Continue only if you know what you're doing and are okay with the risk!",
            PrimaryButtonText = "Exit",
            CloseButtonText = "Continue anyway",
            CloseButtonAppearance = ControlAppearance.Caution,
        }.ShowDialogAsync();

        if (result is MessageBoxResult.Primary)
        {
            App.Current.Shutdown();
        }
    }

    public static string? Truncate(this string? str, int maxLength)
    {
        if (str is not null)
        {
            int snipLength = str.Length <= maxLength
                ? str.Length
                : maxLength;

            return str[0..snipLength];
        }

        return null;
    }

    public static string Redact(this string? data, string replacement = FileLogger.USER_REPLACEMENT_STRING)
    {
        return data?.Replace(Environment.UserName, replacement) ?? string.Empty;
    }

    public static void InvertVisibility(this UIElement elm)
    {
        if (elm.Visibility is Visibility.Visible)
        {
            elm.Visibility = Visibility.Collapsed;
            return;
        }

        elm.Visibility = Visibility.Visible;
    }

    public static string PluralizeIf(this string word, bool condition, bool knownToBeSingular = false)
    {
        return condition
            ? word.Pluralize(knownToBeSingular)
            : word;
    }

    public static string PluralizeIfMany(this string word, int count, bool knownToBeSingular = false)
    {
        return PluralizeIf(word, count is not 1, knownToBeSingular);
    }

    [DebuggerHidden]
    [SuppressMessage("Major Bug", "S3343:Caller information parameters should come at the end of the parameter list", Justification = "No.")]
    public static async Task TimeAsync(this Func<Task> action, [CallerMemberName] string? tag = null, string timeFormat = DEFAULT_MS_STRING_FORMAT)
    {
        await TimeCoreAsync(action, tag, timeFormat);
    }

    [DebuggerHidden]
    [SuppressMessage("Major Bug", "S3343:Caller information parameters should come at the end of the parameter list", Justification = "No.")]
    public static async Task<T> TimeAsync<T>(this Func<Task<T>> action, [CallerMemberName] string? tag = null, string timeFormat = DEFAULT_MS_STRING_FORMAT)
    {
        return await TimeCoreAsync(action, tag, timeFormat, true);
    }

    [DebuggerHidden]
    [SuppressMessage("Major Bug", "S3343:Caller information parameters should come at the end of the parameter list", Justification = "No.")]
    public static void Time(this Action action, [CallerMemberName] string? tag = null, string timeFormat = DEFAULT_MS_STRING_FORMAT)
    {
        _ = TimeCoreSync(() =>
        {
            action();
            return new object();
        }, tag, timeFormat);
    }

    [DebuggerHidden]
    [SuppressMessage("Major Bug", "S3343:Caller information parameters should come at the end of the parameter list", Justification = "No.")]
    public static T Time<T>(this Func<T> action, [CallerMemberName] string? tag = null, string timeFormat = DEFAULT_MS_STRING_FORMAT)
    {
        return TimeCoreSync(action, tag, timeFormat);
    }

    private static async Task<TResult> TimeCoreAsync<TResult>(Func<Task<TResult>> action, string? tag, string timeFormat, bool isAsync)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            FileLogger.Log($"--- {tag}: Start");
            if (isAsync)
            {
                return await action();
            }
            else
            {
                _ = action.Invoke();
                return default!;
            }
        }
        finally
        {
            sw.Stop();
            string formattedTime = sw.ElapsedMilliseconds.ToString(timeFormat);
            FileLogger.Log($"--- {tag}: {formattedTime}ms");
        }
    }

    private static async Task TimeCoreAsync(Func<Task> action, string? tag, string timeFormat)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            FileLogger.Log($"--- {tag}: Start");
            await action();
        }
        finally
        {
            sw.Stop();
            string formattedTime = sw.ElapsedMilliseconds.ToString(timeFormat);
            FileLogger.Log($"--- {tag}: {formattedTime}ms");
        }
    }

    private static TResult TimeCoreSync<TResult>(Func<TResult> action, string? tag, string timeFormat)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            FileLogger.Log($"--- {tag}: Start");
            return action();
        }
        finally
        {
            sw.Stop();
            string formattedTime = sw.ElapsedMilliseconds.ToString(timeFormat);
            FileLogger.Log($"--- {tag}: {formattedTime}ms");
        }
    }
}

public sealed class MethodDurationWatch : IDisposable
{
#if DEBUG
    private readonly Stopwatch _stopwatch;
    private readonly string _caller;

    public MethodDurationWatch(string caller)
    {
        _stopwatch = Stopwatch.StartNew();
        _caller = caller;
    }

    private bool _disposed = false;
    public void Dispose()
    {
        if (!_disposed)
        {
            _stopwatch.Stop();
            ConsoleAbstraction.WriteLine($"[[[magenta]{_caller}[/]]]: [yellow]{_stopwatch.ElapsedMilliseconds:n0}[/] ms");
            _disposed = true;
        }
    }

    public static IDisposable BeginScope([CallerMemberName] string caller = "Method Timer")
    {
        return new MethodDurationWatch(caller);
    }
#else
    public static IDisposable? BeginScope(string? caller = null)
    {
        return null;
    }

    public void Dispose()
    {
    }
#endif
}

public static class HeapTracker
{
    public static unsafe int HeapSize<T>(in T t) where T : class
    {
#if DEBUG
        MethodTable* methodTable = (MethodTable*)typeof(T).TypeHandle.Value;

        if (typeof(T).IsArray)
        {
            Array arr = (t as Array)!;
            return (int)methodTable->m_BaseSize + (arr.Length * methodTable->m_dwFlags.m_componentSize);
        }

        return (t is string str)
            ? (int)methodTable->m_BaseSize + (str.Length * methodTable->m_dwFlags.m_componentSize)
            : (int)methodTable->m_BaseSize;
#else
        return 0;
#endif
    }

#if DEBUG
    [StructLayout(LayoutKind.Explicit)]
    internal struct DWFlags
    {
        [FieldOffset(0)]
        internal ushort m_componentSize;

        [FieldOffset(2)]
        internal ushort m_flags;
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct MethodTable
    {
        [FieldOffset(0)]
        internal DWFlags m_dwFlags;

        [FieldOffset(4)]
        internal uint m_BaseSize;
    }
#endif
}

#if DEBUG
internal class ConsoleDebugLogger : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return new LogScope(state);
    }

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        LogLevel level = FileLogger.MicrosoftLogLevelToLocal(logLevel);
        string color = FileLogger.LogLevelColors.GetAnsiColor(level);

        string message = $"[[{color}{logLevel}[/]]]: {LogScopeManager.PrependScopes(formatter(state, null)).EscapeMarkup()}";

        AnsiConsole.MarkupLine(message);

        if (exception is not null)
        {
            AnsiConsole.WriteException(exception);
        }
    }
}
#endif

#pragma warning disable IDE0079 // Remove unnecessary suppression

/// <summary>
/// Specifies that this method is called by generated code and cannot be marked as static.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
[SuppressMessage("Minor Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "Method is called by generated code.")]
[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Method is called by generated code.")]
[Conditional("CODE_ANALYSIS")]
public sealed class CalledByGeneratedAttribute : Attribute
{
}

/// <summary>
/// Specifies that this method is called by generated code and cannot be marked as static.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
[SuppressMessage(
    "Minor Code Smell",
    "CS8618:Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.",
    Justification = "Method is called by the Visual Designer, not runtime code.")]
[Conditional("CODE_ANALYSIS")]
public sealed class ConstructorForDesignerAttribute : Attribute
{
}

#pragma warning restore IDE0079 // Remove unnecessary suppression
