namespace APKognito.ApkLib;

public readonly struct ProgressInfo(string data, ProgressUpdateType type)
{
    public readonly string Data = data;

    public readonly ProgressUpdateType UpdateType = type;
}

public enum ProgressUpdateType : byte
{
    Content,
    Title,
}
