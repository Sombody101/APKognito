using Newtonsoft.Json;

namespace APKognito.Models;

public record JavaDownloadInfo
{
    public Version JavaVersion { get; init; }

    public string DownloadUrl { get; init; }

    public long DownloadSize { get; init; }

    public string? InstallDirectory { get; init; }

    public bool LTS { get; }

    [JsonConstructor]
    public JavaDownloadInfo(string version, string downloadUrl, long downloadSize)
    {
        if (version.EndsWith("0.LTS", StringComparison.OrdinalIgnoreCase))
        {
            version = version[..^6];
            LTS = true;
        }

        // Fuckass Java maintainers and their fuckass versions
        int indexCount = version.Count(c => c is '.');

        if (indexCount >= 3 && version.Contains('+'))
        {
            // X.X.X.(patch * 100) + (build)
            // 18.0.2.1+1 -> 18.0.2.101

            int lastIndex = version.LastIndexOf('.');
            string buildInfo = version[lastIndex..];

            string[] pair = buildInfo.Split('+');
            int patch = int.Parse(pair[0]);
            int build = int.Parse(pair[1]);

            string trimmedVersion = version[..lastIndex];
            JavaVersion = Version.Parse($"{trimmedVersion}.{(patch * 100) + build}");
        }
        else
        {
            JavaVersion = new Version(version.Replace('+', '.'));
        }


        DownloadUrl = downloadUrl;
        DownloadSize = downloadSize;
    }
}
