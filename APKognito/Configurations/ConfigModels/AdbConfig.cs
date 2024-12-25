using Newtonsoft.Json;
using System.Collections;
using System.IO;

namespace APKognito.Configurations.ConfigModels;

[ConfigFile("adb-config.json", ConfigType.Json, ConfigModifiers.JsonIndented | ConfigModifiers.JsonIgnoreMissing)]
internal sealed class AdbConfig : IKognitoConfig
{
    /// <summary>
    /// Defaults to the platform tools installed with Android Studio
    /// </summary>
    [JsonProperty("adb_path")]
    public string PlatformToolsPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android\\Sdk\\platform-tools");

    [JsonProperty("cached_device_id")]
    public string? CurrentDeviceId { get; set; } = null;

    [JsonProperty("adb_devices")]
    public List<AdbDeviceInfo> AdbDevices { get; set; } = [];

    [JsonProperty("defined_cmdlets")]
    public Dictionary<string, string> UserCmdlets { get; private set; } = [];

    public AdbDeviceInfo? GetCurrentDevice(string? deviceId = null)
    {
        deviceId ??= CurrentDeviceId;

        if (deviceId is null)
        {
            return null;
        }

        return AdbDevices.Find(device => device.DeviceId == deviceId);
    }
}

public sealed class AdbDeviceInfo
{
    [JsonIgnore]
    public static readonly byte[] DefaultIp = [0, 0, 0, 0];

    [JsonProperty("device_id")]
    public string DeviceId { get; set; }

    [JsonProperty("device_type")]
    public DeviceType DeviceType { get; set; }

    [JsonProperty("device_paths")]
    public DevicePaths InstallPaths { get; set; }

    [JsonProperty("device_local_ip")]
    public byte[] IpAddress { get; set; } = DefaultIp;

    [JsonIgnore]
    public bool ConnectedByLan => StructuralComparisons.StructuralEqualityComparer.Equals(IpAddress, new byte[0, 0, 0, 0]);

    public AdbDeviceInfo(string id, DeviceType type, string ovrd = "")
    {
        DeviceId = id;
        SetDeviceType(type, ovrd);
    }

    public void SetDeviceType(DeviceType deviceType, string ovrd = "")
    {
        if (deviceType == DeviceType)
        {
            return;
        }

        DeviceType = deviceType;

        InstallPaths = deviceType is DeviceType.UserOverridePaths
            ? new(ovrd)
            : new(deviceType);
    }
}

public struct DevicePaths
{
    [JsonProperty("device_obb_path")]
    public string ObbPath { get; set; }

    public DevicePaths(DeviceType deviceType, string ovrd = "")
    {
        switch (deviceType)
        {
            case DeviceType.BasicAndroid:
                ObbPath = "/data/media/obb";
                return;

            case DeviceType.MetaQuest:
                ObbPath = "/sdcard/Android/obb/";
                return;

            case DeviceType.UserOverridePaths:
                ObbPath = ovrd;
                break;

            default:
                throw new ArgumentException($"There is no case for {deviceType}");
        }
    }

    [JsonConstructor]
    public DevicePaths(string overrideObbPath)
    {
        ObbPath = overrideObbPath;
    }
}

public enum DeviceType
{
    None,
    BasicAndroid,
    MetaQuest,

    // More will be added if there's more device types

    UserOverridePaths,
}