using Humanizer;
using System.Runtime.InteropServices;

namespace APKognito.Utilities;

internal static class Tools
{
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
        return data?.Replace(Environment.UserName, FileLogger.ReplacementUsername) ?? string.Empty;
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
}
