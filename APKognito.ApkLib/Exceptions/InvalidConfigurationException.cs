using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace APKognito.ApkLib.Exceptions;

public sealed class InvalidConfigurationException(string message) : Exception(message)
{
    private const string DEFAULT_CONFIG_MESSAGE = "The required config type was null.";

    [DebuggerHidden]
    public static void ThrowIfNull<T>([NotNull] T? obj, string message = DEFAULT_CONFIG_MESSAGE)
    {
        if (obj is null)
        {
            ThrowInvalidConfigException(message, typeof(T));
        }
    }

    [DebuggerHidden]
    public static void ThrowIfNullEmptyOrWhitespace([NotNull] string? obj, string message)
    {
        if (string.IsNullOrWhiteSpace(obj))
        {
            throw new InvalidConfigurationException(message);
        }
    }

    [DoesNotReturn]
    private static void ThrowInvalidConfigException(string message)
    {
        throw new InvalidConfigurationException(message);
    }

    [DoesNotReturn]
    private static void ThrowInvalidConfigException(string message, Type objType)
    {
        throw new InvalidConfigurationException($"{objType.Name} is null: {message}");
    }
}
