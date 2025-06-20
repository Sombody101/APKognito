namespace APKognito.ApkLib.Interfaces;

// Marked as internal so people don't try gathering or abstracting all types of editors using it.
// Some editors won't implement this interface, but will likely still have a `Run()` method.
internal interface IRunnable<out T>
{
    public T Run();
}
