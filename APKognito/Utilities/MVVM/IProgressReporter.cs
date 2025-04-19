namespace APKognito.Utilities.MVVM;

public interface IProgressReporter
{
    public event EventHandler<ProgressUpdateEventArgs> ProgressChanged;

    void ReportUpdate(string update, ProgressUpdateType updateType = ProgressUpdateType.Content);

    void ForwardUpdate(ProgressUpdateEventArgs args);
}

public class ProgressUpdateEventArgs(string update, ProgressUpdateType updateType) : EventArgs
{
    public string UpdateValue { get; } = update;

    public ProgressUpdateType UpdateType { get; } = updateType;
}

public enum ProgressUpdateType
{
    Content,
    Title,
}