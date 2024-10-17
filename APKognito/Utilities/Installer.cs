using APKognito.ViewModels.Pages;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;

namespace APKognito.Utilities;

internal static class Installer
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
    public static async Task<string?> FetchAsync(string url, int num = 0)
    {
        var result = await FetchParseDocument(url, [[0, "assets", num, "browser_download_url"]]);

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
    public static async Task<string?[]> FetchAsync(string url, params object[][] indexes)
    {
        return await FetchParseDocument(url, indexes) as string?[] ?? [];
    }

    public static async Task<bool> DownloadAsync(string url, string name)
    {
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

    public static async Task<bool> FetchAndDownload(string url, string name, int num = 0)
    {
        string? downloadUrl = await FetchAsync(url, num);

        if (downloadUrl is null)
        {
            return false;
        }

        return await DownloadAsync(downloadUrl, name);
    }

    private static async Task<object?> FetchParseDocument(string url, object[][] indexes)
    {
        if (indexes.Length == 0)
        {
            return null;
        }

        string? jsonResult = null;
        string?[] output = new string[indexes.Length];

        try
        {
            HttpResponseMessage response = await App.SharedHttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            jsonResult = await response.Content.ReadAsStringAsync();

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
}
