namespace APKognito.ApkLib;

public readonly struct ProgressInfo
{
    public readonly string Data;

    public readonly ProgressUpdateType UpdateType;

    public ProgressInfo(string data, ProgressUpdateType type)
    {
        Data = data;
        UpdateType = type;
    }
}

public enum ProgressUpdateType : byte
{
    Content,
    Title,
}
