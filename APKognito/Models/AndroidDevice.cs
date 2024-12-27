namespace APKognito.Models;

public class AndroidDevice
{
    public static readonly AndroidDevice Empty = new();

    public int BatteryLevel { get; init; } = -1;

    public float FreeSpace { get; init; } = 0;
    public float UsedSpace { get; init; } = 0;
    public float TotalSpace { get; init; } = 0;
}
