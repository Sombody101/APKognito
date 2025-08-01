using System.Diagnostics;

namespace APKognito.Utilities;

/// <summary>
/// This exception is only to test how APKognito handles random exceptions. 
/// It will prevent compilation on release builds. 
/// </summary>
public class DebugOnlyException : Exception
{
#if DEBUG
    public DebugOnlyException()
        : base("This exception can only be used while debugging. If you see this message, you are using a debug build which will be more prone to bugs or errors.")
    {
    }
#endif

    private DebugOnlyException(string message)
        : base(message)
    {
    }

    [Conditional("DEBUG")]
    public static void Assert([DoesNotReturnIf(false)] bool check, string message)
    {
        if (!check)
        {
            throw new DebugOnlyException(message);
        }
    }
}
