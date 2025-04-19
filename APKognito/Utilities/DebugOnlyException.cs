// Using a preprocessor statement encourages cleanup since all usages will be errors on non-debug builds.
#if DEBUG

namespace APKognito.Utilities;
/// <summary>
/// This exception is only to test how APKognito handles random exceptions. 
/// It will prevent compilation on release builds. 
/// </summary>
public class DebugOnlyException : Exception
{
    public DebugOnlyException()
        : base("This exception can only be used while debugging. If you see this message, you are using a debug build which will be more prone to bugs or errors.")
    {
    }

    private DebugOnlyException(string message)
        : base(message)
    {
    }

    public static void Assert([DoesNotReturnIf(true)] bool check, string message)
    {
        if (!check)
        {
            throw new DebugOnlyException(message);
        }
    }
}

#endif