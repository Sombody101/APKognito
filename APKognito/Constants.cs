namespace APKognito;

#pragma warning disable S1075 // URIs should not be hardcoded

internal static class Constants
{
    public const string UpdateInstalledArgument = "[::updated::]";

    public const string POWERSHELL_PATH = "C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe";

    /*
     * URLs
     */

    public const string APL_SIGNER_URL_LTST = "https://api.github.com/repos/patrickfav/uber-apk-signer/releases/latest";
    public const string APKTOOL_JAR_URL_LTST = "https://api.github.com/repos/iBotPeaches/apktool/releases/latest";
    public const string APKTOOL_BAT_URL = "https://raw.githubusercontent.com/iBotPeaches/Apktool/master/scripts/windows/apktool.bat";
    public const string ZIPALIGN_URL = "https://raw.githubusercontent.com/Sombody101/APKognito/master/BinTools/zipalign.exe";

    public const string ADB_INSTALL_URL = "https://dl.google.com/android/repository/platform-tools-latest-windows.zip";

    public const string GITHUB_API_URL = "https://api.github.com/repos/Sombody101/APKognito/releases";
    public const string GITHUB_API_URL_LATEST = $"{GITHUB_API_URL}/latest";
}
