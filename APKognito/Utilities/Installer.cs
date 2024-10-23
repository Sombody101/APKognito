using APKognito.ViewModels.Pages;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace APKognito.Utilities;

internal static partial class Installer
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
    public static async Task<string?> FetchAsync(string url, CancellationToken cToken, int num = 0)
    {
        var result = await FetchParseDocument(url, [[0, "assets", num, "browser_download_url"]], cToken);

        if (result is string[] strArray)
        {
            return strArray[0];
        }

        return null;
    }

    /// <summary>
    /// Fetches a JSON document and retrieves values from the given paths.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="indexes"></param>
    /// <returns></returns>
    public static async Task<string?[]> FetchAsync(string url, CancellationToken cToken, params object[][] indexes)
    {
        return await FetchParseDocument(url, indexes, cToken) as string?[] ?? [];
    }

    public static async Task<bool> DownloadAsync(string url, string name, CancellationToken cToken)
    {
        if (!await VerifyConnection())
        {
            return false;
        }

        try
        {
            string fileName = Path.GetFileName(name);
            HomeViewModel.Log($"Fetching {fileName}");
            using HttpResponseMessage response = await App.SharedHttpClient.GetAsync(url);
            _ = response.EnsureSuccessStatusCode();

            using FileStream fileStream = File.Create(name);
            HomeViewModel.Log($"Installing {fileName}");
            await response.Content.CopyToAsync(fileStream);

            return true;
        }
        catch (HttpRequestException ex)
        {
            HomeViewModel.LogError($"Unable to download a tool: {ex.Message}");
            FileLogger.LogException(ex);
        }
        catch (Exception ex)
        {
            HomeViewModel.LogError($"An error occurred: {ex.Message}");
            FileLogger.LogException(ex);
        }

        return false;
    }

    public static async Task<bool> FetchAndDownload(string url, string name, CancellationToken cToken, int assetIndex = 0)
    {
        string? downloadUrl = await FetchAsync(url, cToken, assetIndex);

        if (downloadUrl is null)
        {
            return false;
        }

        return await DownloadAsync(downloadUrl, name, cToken);
    }

    private static async Task<object?> FetchParseDocument(string url, object[][] indexes, CancellationToken cToken)
    {
        if (!await VerifyConnection())
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
            response.EnsureSuccessStatusCode();

            jsonResult = await response.Content.ReadAsStringAsync(cToken);

            JToken originalToken = JArray.Parse(jsonResult);

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
                        HomeViewModel.LogError($"Failed to find '{index}' in JSON response.");
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
            HomeViewModel.LogError($"Failed to fetch JSON: {ex.Message}");
            FileLogger.LogException(ex);
        }
        catch (Exception ex)
        {
            HomeViewModel.LogError($"An error occurred: {ex.Message}");
            FileLogger.LogException(ex);
        }

        FileLogger.LogError($"Fetched JSON snippet: {jsonResult.Truncate(1500) ?? "[NULL]"}");
        return null;
    }

    private static async Task<bool> VerifyConnection()
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
                    HomeViewModel.LogError("No network device found. A WiFi adapter or ethernet is required.");
                    return false;

                case 2:
                    HomeViewModel.LogError($"Failed to ping Cloudflare DNS (1.1.1.1). IP Status: {statusName}");
                    return false;

                case 3:
                    HomeViewModel.LogError($"Failed to ping Cloudflare (https://www.cloudflare.com/). IP Status: {statusName}");
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
