using APKognito.Models;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using Newtonsoft.Json.Linq;

namespace APKognito.ViewModels.Windows;

public sealed partial class JavaInstallationSelectionViewModel : KognitoWindowViewModel
{
    private const string ADOPTIUM_JDK_RELEASES_URL = "https://api.adoptium.net/v3";
    private readonly IViewLogger _logger;

    #region Properties

    [ObservableProperty]
    public partial JavaDownloadInfo[] JavaInstallationCollection { get; private set; } = Array.Empty<JavaDownloadInfo>();

    [ObservableProperty]
    public partial JavaDownloadInfo? SelectedInstallation { get; set; }

    [ObservableProperty]
    public partial bool UseCustomInstallPath { get; set; } = false;

    [ObservableProperty]
    public partial string CustomInstallPath { get; set; }

    #endregion Properties

#if DEBUG
    public JavaInstallationSelectionViewModel()
    {
        // For debug
    }
#endif

    public JavaInstallationSelectionViewModel(IViewLogger logger)
    {
        _logger = logger;
    }

    #region Commands

    [RelayCommand]
    private async Task LoadJdkVersionsCommandAsync(CancellationToken token)
    {
        try
        {
            Log("Fetching installer list...");

            string?[] releases = await WebGet.FetchAsync($"{ADOPTIUM_JDK_RELEASES_URL}/info/available_releases", this, token, ["available_releases"]);

            if (releases.Length is 0)
            {
                throw new InvalidOperationException("Returned JSON document was empty or in an unexpected format.");
            }

            JArray root = JArray.Parse(releases[0]!);
            IEnumerable<Task<string?>> fetchTasks = root.Select(async v =>
            {
                int version = v.Value<int>();

                if (version < 11)
                {
                    // JDK 8 is just a bitch to deal with
                    return null;
                }

                string url = $"{ADOPTIUM_JDK_RELEASES_URL}/assets/latest/{version}/hotspot?image_type=jdk&architecture=x64&os=windows";

                return (await WebGet.FetchAsync(url, this, token, [0]))[0];
            });

            string?[] installerDatas = await Task.WhenAll(fetchTasks);

            IEnumerable<JavaDownloadInfo?> parsedInfos = installerDatas.Select(o =>
            {
                if (o is null)
                {
                    return null;
                }

                var jobject = JObject.Parse(o);

                return new JavaDownloadInfo(
                    jobject.SelectToken("version.semver")?.ToString() ?? "0.0.0",
                    jobject.SelectToken("binary.installer.link")?.ToString() ?? string.Empty,
                    jobject.SelectToken("binary.installer.size")?.Value<long>() ?? 0
                );
            });

            JavaInstallationCollection = [.. parsedInfos.Where(i => i is not null).Reverse()];
        }
        catch (OperationCanceledException)
        {
            LogError("Fetch was cancelled.");
        }
        catch (Exception ex)
        {
            LogError($"Unexpected error while fetching JDK/JRE downloads list: {ex.Message}");
            FileLogger.LogException(ex);
        }
    }

    public async Task<JavaDownloadInfo?> FetchDownloadUrlAsync(JavaDownloadInfo installerInfo, CancellationToken token)
    {
        try
        {
            LogDebug("Fetching installer URL...");

            string rawInstallersList = (await WebGet.FetchAsync($"{ADOPTIUM_JDK_RELEASES_URL}/{installerInfo.JavaVersion.Major}", this, token, ["artifacts"]))[0]!;

            JToken? windowsInstaller = JArray.Parse(rawInstallersList)?.FirstOrDefault(i =>
                i["osFamily"]?.ToString() is "windows"
                && i["architecture"]?.ToString() is "x64"
                && i["packageType"]?.ToString() is "exe"
            );

            if (windowsInstaller is null)
            {
                LogError($"Failed to find x64 Windows installer for JDK {installerInfo.JavaVersion.Major} in downloaded version listing.");
                return null;
            }

            string? downloadUrl = windowsInstaller["downloadUrl"]?.ToString();
            long downloadSize = windowsInstaller["downloadFileSizeInBytes"]?.Value<long>() ?? 0;

            return installerInfo with
            {
                DownloadUrl = downloadUrl,
                DownloadSize = downloadSize
            };
        }
        catch (OperationCanceledException)
        {
            LogError("Fetch was cancelled.");
        }
        catch (Exception ex)
        {
            LogError($"Unexpected error while fetching JDK/JRE downloads list: {ex.Message}");
            FileLogger.LogException(ex);
        }

        return null;
    }

    #endregion Commands

    protected override void AppendEntry(LogBoxEntry entry)
    {
        _logger.AppendLog(entry);
    }
}
