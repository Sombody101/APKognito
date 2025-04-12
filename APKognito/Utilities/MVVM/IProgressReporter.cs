namespace APKognito.Utilities.MVVM;

public interface IProgressReporter
{
    public event EventHandler<ProgressUpdateEventArgs> ProgressChanged;

    void ReportUpdate(string update, UpdateType updateType = UpdateType.Content);

    void ForwardUpdate(ProgressUpdateEventArgs args);
}

public class ProgressUpdateEventArgs(string update, UpdateType updateType) : EventArgs
{
    public string UpdateValue { get; } = update;

    public UpdateType UpdateType { get; } = updateType;
}

public enum UpdateType
{
    Content,
    Title,
}