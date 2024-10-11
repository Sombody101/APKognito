namespace APKognito;

internal class RenameFailedException(string errorMessage) : Exception(errorMessage)
{
}