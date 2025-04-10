namespace APKognito.Exceptions;

public class RenameFailedException(string errorMessage) : Exception(errorMessage)
{
}