namespace APKognito.Exceptions;

public class AdbPushFailedException : Exception
{
    public AdbPushFailedException(string apkName, string reason)
        : base($"Failed to push to device: {reason}")
    { }
}
