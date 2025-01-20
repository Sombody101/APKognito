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

/// <summary>
/// Information for a given ADB device/
/// </summary>
public sealed class AdbDeviceInfo
{
    [JsonIgnore]
    public static readonly byte[] DefaultIp = [0, 0, 0, 0];

    [JsonProperty("device_id")]
    public string DeviceId { get; set; }

    [JsonProperty("device_name")]
    public string DeviceName { get; set; }

    [JsonProperty("device_local_ip")]
    public byte[] IpAddress { get; set; } = DefaultIp;

    [JsonIgnore]
    public bool DeviceAuthorized { get; set; } = false;

    [JsonIgnore]
    public bool ConnectedByLan => StructuralComparisons.StructuralEqualityComparer.Equals(IpAddress, DefaultIp);

    public AdbDeviceInfo(string id)
    {
        DeviceId = id;
    }
}