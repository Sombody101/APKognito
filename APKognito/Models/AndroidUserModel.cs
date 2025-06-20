using APKognito.Utilities;

namespace APKognito.Models;

public record AndroidUserModel
{
    public string Username { get; }

    public int UserId { get; }

    public int UserFlags { get; }

    // Likely won't be used
    // public bool IsRunning { get; }

    /// <summary>
    /// Parses a UserInfo dataset according to the Android UserInfo spec (<see langword="UserInfo.toString()"/>)
    /// <seealso href="https://android.googlesource.com/platform/frameworks/base/+/master/core/java/android/content/pm/UserInfo.java#528">Android Source</seealso>
    /// </summary>
    /// <param name="adbUserText"></param>
    public AndroidUserModel(string adbUserText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adbUserText);

        // UserInfo{...} <state> -> [UserInfo{...}, <state>]
        string[] statusSplit = adbUserText.Trim().Split(' ');

        if (statusSplit.Length != 2)
        {
            throw new ArgumentException($"UserInfo pair is {statusSplit.Length}, not the expected two.");
        }

        // Remove "UserInfo{" prefix and "}" suffix
        string userInfo = statusSplit[0].Trim()[9..^2];

        /*
         * [0]: id
         * [1]: username
         * [2]: flags
         */

        string[] dataPairs = userInfo.Split(':');

        Username = dataPairs[0];

        if (int.TryParse(dataPairs[1], out int id))
        {
            UserId = id;
        }

        if (int.TryParse(dataPairs[2], out int flags))
        {
            UserFlags = flags;
        }
    }
}
