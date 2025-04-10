namespace APKognito.Utilities;

// Using a preprocessor statement encourages cleanup since all usages will be errors.
#if DEBUG
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

    public DebugOnlyException(string l)
        : base(l)
    {
    }
}
#endif

public static class Utils
{
    public static void Assert([DoesNotReturnIf(true)] this bool check, string l)
    {
        if (!check)
        {
#if DEBUG
            throw new DebugOnlyException(l);
#else
            throw new InvalidOperationException(l);
#endif
        }
    }
}