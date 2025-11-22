namespace APKognito.Models;

public record AndroidDevice
{
    public static readonly AndroidDevice Empty = new();

    public string DeviceName { get; init; } = string.Empty;

    public int BatteryLevel { get; init; } = -1;

    public float FreeSpace { get; init; } = 0;
    public float UsedSpace { get; init; } = 0;
    public float TotalSpace { get; init; } = 0;
}
