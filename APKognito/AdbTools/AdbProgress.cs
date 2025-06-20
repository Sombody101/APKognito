using System.Globalization;
using System.Text.RegularExpressions;

namespace APKognito.AdbTools;

public partial record AdbProgress
{
    private const string R_UPDATE_PERCENT = "update_percent",
        R_DEVICE_PATH = "device_path",
        R_FILES_SENT = "files_sent",
        R_FILES_SKIPPED = "files_skipped",
        R_TRANSFER_SPEED = "transfer_speed",
        R_BYTES_TRANSFERRED = "bytes_transferred",
        R_TRANSFER_TIME = "transfer_time";

    public AdbProgressType ProgressType { get; }

    /*
     * Update properties
     */

    public string? DeviceFullFileName { get; }

    public int UpdatePercent { get; }

    /*
     * Finish properties
     */

    public int FilesSent { get; }

    public int FilesSkipped { get; }

    public string? TransferSpeed { get; }

    public ulong TotalBytesTransferred { get; }

    public float TransferTime { get; }

    public AdbProgress(string stdOutLine, AdbProgressType progressType)
    {
        stdOutLine = stdOutLine.Trim();

        if (progressType is AdbProgressType.Auto)
        {
            progressType = DetectProgressType(stdOutLine);
        }

        ProgressType = progressType;

        // I'd love to extract these cases, but the properties are get-only and I don't feel like adding private setters :p
        switch (progressType)
        {
            case AdbProgressType.Update:
                {
                    Match parsedLine = ParseAdbResponse(stdOutLine, AdbUpdateOutputRegex());

                    DeviceFullFileName = parsedLine.Groups[R_DEVICE_PATH].Value;
                    UpdatePercent = ParseNumber<int>(parsedLine.Groups[R_UPDATE_PERCENT].Value);
                }
                break;

            case AdbProgressType.Finish:
                {
                    Match parsedLine = ParseAdbResponse(stdOutLine, AdbFinishOutputRegex());
                    
                    FilesSent = ParseNumber<int>(parsedLine.Groups[R_FILES_SENT].Value);
                    FilesSkipped = ParseNumber<int>(parsedLine.Groups[R_FILES_SENT].Value);
                    TransferSpeed = parsedLine.Groups[R_TRANSFER_SPEED].Value;
                    TotalBytesTransferred = ParseNumber<ulong>(parsedLine.Groups[R_BYTES_TRANSFERRED].Value);
                    TransferTime = ParseNumber<float>(parsedLine.Groups[R_TRANSFER_TIME].Value);
                }
                break;
        }
    }

    private static Match ParseAdbResponse(string line, Regex regex)
    {
        var parsedLine = regex.Match(line);

        if (parsedLine is null || !parsedLine.Success)
        {
            throw new InvalidAdbUpdateResponseException($"Failed to parse ADB response '{line}'");
        }

        return parsedLine;
    }

    private static T ParseNumber<T>(string s) where T : struct, IParsable<T>
    {
        if (T.TryParse(s, CultureInfo.CurrentCulture, out T value))
        {
            return value;
        }

        // It's okay for this to return 0 since any errors should have been caught in the regex parsing
        return default;
    }

    private AdbProgressType DetectProgressType(string line)
    {
        // This is jank, but should work.
        // The only time the line can start with '[' is if it's the final line, because the first segment is the given ADB file path.
        if (line.StartsWith('['))
        {
            return AdbProgressType.Update;
        }

        return AdbProgressType.Finish;
    }

    public enum AdbProgressType
    {
        Auto,
        Update,
        Finish,
    }

    [GeneratedRegex(@$"\[\s?(?<{R_UPDATE_PERCENT}>\d+)%\] (?<{R_DEVICE_PATH}>.*)")]
    private static partial Regex AdbUpdateOutputRegex();

    /// <summary>
    /// Lawd almighty
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@$"(^.*?):\s+(?<{R_FILES_SENT}>\d+) file (?:pushed|pulled),\s+(?<{R_FILES_SKIPPED}>\d+) skipped.\s+(?<{R_TRANSFER_SPEED}>[\d.]+ \w+/s)\s+\((?<{R_BYTES_TRANSFERRED}>\d+) bytes in (?<{R_TRANSFER_TIME}>[\d.]+)s\)$")]
    private static partial Regex AdbFinishOutputRegex();

    public class InvalidAdbUpdateResponseException(string message) : Exception(message)
    {
    }
}
