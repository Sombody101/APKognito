using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Humanizer;

namespace APKognito.Utilities;

internal static class Tools
{
    private const string DEFAULT_MS_STRING_FORMAT = "n";

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

    public static string Redact(this string? data)
    {
        return data?.Replace(Environment.UserName, FileLogger.USER_REPLACEMENT_STRING) ?? string.Empty;
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

    [SuppressMessage("Major Bug", "S3343:Caller information parameters should come at the end of the parameter list", Justification = "No.")]
    public static async Task TimeAsync(this Func<Task> action, [CallerMemberName] string? tag = null, string timeFormat = DEFAULT_MS_STRING_FORMAT)
    {
        await TimeCoreAsync(action, tag, timeFormat);
    }

    [SuppressMessage("Major Bug", "S3343:Caller information parameters should come at the end of the parameter list", Justification = "No.")]
    public static async Task<T> TimeAsync<T>(this Func<Task<T>> action, [CallerMemberName] string? tag = null, string timeFormat = DEFAULT_MS_STRING_FORMAT)
    {
        return await TimeCoreAsync(action, tag, timeFormat, true);
    }

    [SuppressMessage("Major Bug", "S3343:Caller information parameters should come at the end of the parameter list", Justification = "No.")]
    public static void Time(this Action action, [CallerMemberName] string? tag = null, string timeFormat = DEFAULT_MS_STRING_FORMAT)
    {
        _ = TimeCoreSync(() => { action(); return new object(); }, tag, timeFormat);
    }

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
            FileLogger.Log($"--- {tag}: {formattedTime}");
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
            FileLogger.Log($"--- {tag}: {formattedTime}");
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
            FileLogger.Log($"--- {tag}: {formattedTime}");
        }
    }
}

#pragma warning disable IDE0079 // Remove unnecessary suppression

/// <summary>
/// Specifies that this method is called by generated code and cannot be marked as static.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
[SuppressMessage("Minor Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "Method is called by generated code.")]
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
public sealed class ConstructorForDesignerAttribute : Attribute
{
}

#pragma warning restore IDE0079 // Remove unnecessary suppression
