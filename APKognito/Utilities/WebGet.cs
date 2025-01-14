using Newtonsoft.Json.Linq;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace APKognito.Utilities;

public static partial class WebGet
{
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
    public static async Task<string?> FetchAsync(string url, LoggableObservableObject? logger, CancellationToken cToken, int num = 0)
    {
        object? result = await FetchParseDocument(url, [[0, "assets", num, "browser_download_url"]], logger, cToken);

        return result is string[] strArray ? strArray[0] : null;
    }

    /// <summary>
    /// Fetches a JSON document and retrieves values from the given paths.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="indexes"></param>
    /// <returns></returns>
    public static async Task<string?[]> FetchAsync(string url, LoggableObservableObject? logger, CancellationToken cToken, params object[][] indexes)
    {
        return await FetchParseDocument(url, indexes, logger, cToken) as string?[] ?? [];
    }

    public static async Task<bool> DownloadAsync(string url, string name, LoggableObservableObject? logger, CancellationToken cToken)
    {
        if (!await VerifyConnection(logger))
        {
            return false;
        }

        try
        {
            string fileName = Path.GetFileName(name);
            logger?.Log($"Fetching {fileName}");
            using HttpResponseMessage response = await App.SharedHttpClient.GetAsync(url, cToken);
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

    public static async Task<bool> FetchAndDownload(string url, string name, LoggableObservableObject? logger, CancellationToken cToken, int assetIndex = 0)
    {
        string? downloadUrl = await FetchAsync(url, logger, cToken, assetIndex);

        return downloadUrl is not null && await DownloadAsync(downloadUrl, name, logger, cToken);
    }

    private static async Task<object?> FetchParseDocument(string url, object[][] indexes, LoggableObservableObject? logger, CancellationToken cToken)
    {
        if (!await VerifyConnection(logger))
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
            HttpResponseMessage response = await App.SharedHttpClient.GetAsync(url, cToken);
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
                JToken? lastToken = originalToken;
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
                        FileLogger.LogDebug($"Json token: {lastToken.ToString().Truncate(1500) ?? "[NULL]"}");
                        output[i] = null;
                        break;
                    }

                    lastToken = currentToken;
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

    private static async Task<bool> VerifyConnection(LoggableObservableObject? logger)
    {
        try
        {
            (int result, IPStatus? status) = await IsConnectedToInternet();

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
                case 1:
                    logger?.LogError("No network device found. A WiFi adapter or ethernet is required.");
                    return false;

                case 2:
                    logger?.LogError($"Failed to ping Cloudflare DNS (1.1.1.1). IP Status: {statusName}");
                    return false;

                case 3:
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
    ///     <list type="bullet|number|table">
    ///         <listheader>
    ///             <term>0</term>
    ///             <description>No issues; Internet connection works.</description>
    ///         </listheader>
    ///         <item>
    ///             <term>1</term>
    ///             <description>Got a <see langword="false"/> return from <see cref="InternetGetConnectedState"/>.</description>
    ///         </item>
    ///         <item>
    ///             <term>2</term>
    ///             <description>IP Cloudflare ping test failed</description>
    ///         </item>
    ///         <item>
    ///             <term>3</term>
    ///             <description>DNS Cloudflare ping test failed</description>
    ///         </item>
    ///     </list>
    /// </returns>
    private static async Task<(int, IPStatus?)> IsConnectedToInternet()
    {
        // This was added as an attempt to resolve this issue: https://github.com/Sombody101/APKognito/issues/2

        if (!InternetGetConnectedState(out _, 0))
        {
            return (1, null);
        }

        Ping ping = new();

        // Internet check
        PingReply reply = await ping.SendPingAsync(new IPAddress([1, 1, 1, 1]));
        if (reply.Status is not IPStatus.Success)
        {
            return (2, reply.Status);
        }

        // DNS check
        reply = await ping.SendPingAsync("cloudflare.com", 3000);

        return reply.Status is not IPStatus.Success
            ? ((int, IPStatus?))(3, reply.Status)
            : (0, null);
    }

    [LibraryImport("wininet.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool InternetGetConnectedState(out int Description, int ReservedValue);
}