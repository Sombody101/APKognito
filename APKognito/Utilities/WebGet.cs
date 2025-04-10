using APKognito.Utilities.MVVM;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace APKognito.Utilities;

public static partial class WebGet
{
    private const string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.6446.71 Safari/537.36";

    private static readonly HttpClient _sharedHttpClient = new();

    /// <summary>
    /// An <see cref="HttpClient"/> instance that is shared throughout the application.
    /// </summary>
    public static HttpClient SharedHttpClient => _sharedHttpClient;

    static WebGet()
    {
        _sharedHttpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
    }

    public class InvalidJsonIndexerException : Exception
    {
        public InvalidJsonIndexerException(object indexValue)
            : base($"Invalid JSON index type {indexValue.GetType().Name} [Developer error]")
        {
        }
    }

    /// <summary>
    /// Fetches a JSON document and retrieves values from the given paths.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="num"></param>
    /// <returns></returns>
    public static async Task<string?> FetchGitHubReleaseAsync(string url, LoggableObservableObject? logger, CancellationToken cToken, int num = 0)
    {
        object? result = await FetchParseDocumentAsync(url, [["assets", num, "browser_download_url"]], logger, cToken);

        return result is string[] strArray 
            ? strArray[0] 
            : null;
    }

    /// <summary>
    /// Fetches a JSON document and retrieves values from the given paths.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="indexes"></param>
    /// <returns></returns>
    public static async Task<string?[]> FetchAsync(string url, LoggableObservableObject? logger, CancellationToken cToken, params object[][] indexes)
    {
        return await FetchParseDocumentAsync(url, indexes, logger, cToken) as string?[] ?? [];
    }

    public static async Task<bool> DownloadAsync(string url, string name, LoggableObservableObject? logger, CancellationToken cToken)
    {
        if (!await VerifyConnectionAsync(logger))
        {
            return false;
        }

        try
        {
            string fileName = Path.GetFileName(name);
            logger?.Log($"Fetching {fileName}");
            using HttpResponseMessage response = await _sharedHttpClient.GetAsync(url, cToken);
            _ = response.EnsureSuccessStatusCode();

            await using FileStream fileStream = File.Create(name);
            logger?.Log($"Installing {fileName}");
            await response.Content.CopyToAsync(fileStream, cToken);

            return true;
        }
        catch (HttpRequestException ex)
        {
            logger?.LogError($"Unable to download a tool: {ex.Message}");
            FileLogger.LogException(ex);
        }
        catch (Exception ex)
        {
            logger?.LogError($"An error occurred: {ex.Message}");
            FileLogger.LogException(ex);
        }

        return false;
    }

    public static async Task<bool> FetchAndDownloadGitHubReleaseAsync(string url, string downloadPath, LoggableObservableObject? logger, CancellationToken cToken, int assetIndex = 0)
    {
        string? downloadUrl = await FetchGitHubReleaseAsync(url, logger, cToken, assetIndex);

        return downloadUrl is not null && await DownloadAsync(downloadUrl, downloadPath, logger, cToken);
    }

    private static async Task<object?> FetchParseDocumentAsync(string url, object[][] indexes, LoggableObservableObject? logger, CancellationToken cToken)
    {
        if (!await VerifyConnectionAsync(logger))
        {
            return null;
        }

        if (indexes.Length == 0)
        {
            return null;
        }

        string? jsonResult = null;
        string?[] output = new string[indexes.Length];

        try
        {
            HttpResponseMessage response = await _sharedHttpClient.GetAsync(url, cToken);
            _ = response.EnsureSuccessStatusCode();

            jsonResult = await response.Content.ReadAsStringAsync(cToken);


            JToken originalToken;

            try
            {
                originalToken = JArray.Parse(jsonResult);
            }
            catch
            {
                originalToken = JObject.Parse(jsonResult);
            }

            for (int i = 0; i < indexes.Length; ++i)
            {
                JToken? currentToken = originalToken;

                foreach (object index in indexes[i])
                {
                    currentToken = index switch
                    {
                        int intValue => currentToken[intValue],
                        string stringValue => currentToken[stringValue],
                        _ => throw new InvalidJsonIndexerException(index)
                    };

                    if (currentToken is null)
                    {
                        logger?.LogError($"Failed to find '{index}' in JSON response.");
                        break;
                    }
                }

                output[i] = currentToken?.ToString();
            }

            return output;
        }
        catch (HttpRequestException ex)
        {
            logger?.LogError($"Failed to fetch JSON: {ex.Message}");
            FileLogger.LogException(ex);
        }
        catch (Exception ex)
        {
            logger?.LogError($"An error occurred: {ex.Message}");
            FileLogger.LogException(ex);
        }

        FileLogger.LogError($"Fetched JSON snippet: {jsonResult.Truncate(1500) ?? "[NULL]"}");
        return null;
    }

    private static async Task<bool> VerifyConnectionAsync(LoggableObservableObject? logger)
    {
        try
        {
            (ConnectionStatus result, IPStatus? status) = await IsConnectedToInternetAsync();

            if (result is 0)
            {
                return true;
            }

            // Windows specific error that is not listed is IPStatus
            string? statusName = status == (IPStatus)11050
                ? "GeneralFailure"
                : status.ToString();

            switch (result)
            {
                case ConnectionStatus.NotConnected:
                    logger?.LogError("No network device found. A WiFi adapter or ethernet is required.");
                    return false;

                case ConnectionStatus.IpFailed:
                    logger?.LogError($"Failed to ping Cloudflare DNS (1.1.1.1). IP Status: {statusName}");
                    return false;

                case ConnectionStatus.DnsFailed:
                    logger?.LogError($"Failed to ping Cloudflare (https://www.cloudflare.com/). IP Status: {statusName}");
                    return false;
            }
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
        }

        return false;
    }

    /// <summary>
    /// Tests for internet connection.
    /// </summary>
    /// <returns>
    /// </returns>
    private static async Task<(ConnectionStatus, IPStatus?)> IsConnectedToInternetAsync()
    {
        // This was added as an attempt to resolve this issue: https://github.com/Sombody101/APKognito/issues/2

        if (!InternetGetConnectedState(out _, 0))
        {
            return (ConnectionStatus.NotConnected, null);
        }

        Ping ping = new();

        // Internet check
        PingReply reply = await ping.SendPingAsync(new IPAddress([1, 1, 1, 1]));
        if (reply.Status is not IPStatus.Success)
        {
            return (ConnectionStatus.IpFailed, reply.Status);
        }

        // DNS check
        reply = await ping.SendPingAsync("cloudflare.com", 3000);

        return reply.Status is not IPStatus.Success
            ? (ConnectionStatus.DnsFailed, reply.Status)
            : (ConnectionStatus.Success, null);
    }

    [LibraryImport("wininet.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool InternetGetConnectedState(out int Description, int ReservedValue);

    private enum ConnectionStatus
    {
        Success,
        NotConnected,
        IpFailed,
        DnsFailed,
    }
}