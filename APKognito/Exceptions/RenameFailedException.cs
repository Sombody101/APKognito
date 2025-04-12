namespace APKognito.Exceptions;

public class RenameFailedException(string errorMessage, Exception exception) : Exception(errorMessage, exception)
{
    public RenameFailedException(string errorMessage)
        : this(errorMessage, null!)
    { }
}