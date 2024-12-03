namespace APKognito.Exceptions;

[SuppressMessage("Roslynator", "RCS1194:Implement exception constructors", Justification = "Using inline ctor.")]
public class RenameFailedException(string errorMessage) : Exception(errorMessage)
{
}