namespace APKognito.Base;

public interface IDialogResult<out T> where T : class
{
    public T? DialogResult { get; }
}
