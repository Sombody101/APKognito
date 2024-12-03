using Newtonsoft.Json;
using System.IO;

namespace APKognito.Configurations.ConfigModels;

[ConfigFile("adb-config.json", ConfigType.Json, ConfigModifiers.JsonIndented )]
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
    [JsonProperty("device_id")]
    public string DeviceId { get; set; }

    [JsonProperty("device_type")]
    public DeviceType DeviceType { get; set; }

    [JsonProperty("device_paths")]
    public DevicePaths InstallPaths { get; set; }

    public AdbDeviceInfo(string id, DeviceType type, string ovrd1 = "", string ovrd2 = "")
    {
        DeviceId = id;
        SetDeviceType(type, ovrd1, ovrd2);
    }

    public void SetDeviceType(DeviceType deviceType, string ovrd1 = "", string ovrd2 = "")
    {
        if (deviceType == DeviceType)
        {
            return;
        }

        DeviceType = deviceType;

        InstallPaths = deviceType is DeviceType.UserOverridePaths
            ? new(ovrd1, ovrd2)
            : new(deviceType);
    }
}

public sealed class DevicePaths
{
    [JsonProperty("device_apk_path")]
    public string ApkPath { get; set; }

    [JsonProperty("device_obb_path")]
    public string ObbPath { get; set; }

    public DevicePaths(DeviceType deviceType, string ovrd1 = "", string ovrd2 = "")
    {
        switch (deviceType)
        {
            case DeviceType.BasicAndroid:
                ApkPath = "/data/app/";
                ObbPath = "/data/media/obb";
                return;

            case DeviceType.MetaQuest:
                ApkPath = "/sdcard/Android/data/";
                ObbPath = "/sdcard/Android/obb/";
                return;

            case DeviceType.UserOverridePaths:
                ApkPath = ovrd1;
                ObbPath = ovrd2;
                break;

            default:
                throw new ArgumentException($"There is no case for {deviceType}");
        }
    }

    [JsonConstructor]
    public DevicePaths(string overrideApkPath, string overrideObbPath)
    {
        ApkPath = overrideApkPath;
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