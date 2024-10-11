namespace APKognito.Utilities;

/// <summary>
/// This exception is only to test how APKognito handles random exceptions. 
/// It will prevent compilation on release builds.
/// </summary>

#if DEBUG
public class DebugOnlyException : Exception 
{
    public DebugOnlyException()
        : base("This exception can only be used while debugging. This is a filler message.")
    {
    }
}
#endif