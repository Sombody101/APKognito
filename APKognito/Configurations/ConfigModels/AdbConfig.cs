using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using APKognito.AdbTools;
using APKognito.Utilities.MVVM;
using Newtonsoft.Json;

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

        return deviceId is not null
            ? AdbDevices.Find(device => device.DeviceId == deviceId)
            : null;
    }

    public bool AdbWorks([Optional] string? platformToolsPath, IViewLogger? logger = null)
    {
        platformToolsPath ??= PlatformToolsPath;
        bool isInstalled = Directory.Exists(platformToolsPath) && File.Exists(Path.Combine(platformToolsPath, "adb.exe"));

        if (isInstalled)
        {
            logger?.SnackError("Platform tools are not installed!", "You can:\n" +
                "1. Install them by running ':install-adb' in the Console Page.\n" +
                "2. Verify the Platform Tools path in the ADB Configuration Page.\n" +
                $"3. Install them manually at {AdbManager.PLATFORM_TOOLS_INSTALL_LINK}, then set the path in the ADB Configuration Page.");
        }

        return isInstalled;
    }
}

/// <summary>
/// Information for a given ADB device
/// </summary>
public sealed record AdbDeviceInfo
{
    [JsonIgnore]
    public static readonly byte[] DefaultIp = [0, 0, 0, 0];

    [JsonProperty("device_id")]
    public string DeviceId
    {
        get;
        set
        {
            field = value;
            DeviceHashId = GetUlongHash(value);
        }
    }

    [JsonProperty("device_name")]
    public string DeviceName { get; set; } = string.Empty;

    [JsonProperty("device_local_ip")]
    public byte[] IpAddress { get; set; } = DefaultIp;

    [JsonIgnore]
    public bool DeviceAuthorized { get; set; } = false;

    [JsonIgnore]
    public bool ConnectedByLan => StructuralComparisons.StructuralEqualityComparer.Equals(IpAddress, DefaultIp);

    [JsonIgnore]
    public ulong DeviceHashId { get; private set; }

    public AdbDeviceInfo(string id)
    {
        DeviceId = id;
    }

    public static ulong GetUlongHash(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return 0;
        }

        int hash1 = input.GetHashCode();
        int hash2 = input.Substring(input.Length / 2).GetHashCode();

        return ((ulong)hash1 << 32) | (uint)hash2;
    }
}
