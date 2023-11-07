using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace UnoraLaunchpad;

public sealed class HttpService
{
    private readonly HttpClient _httpClient = new();

    public async Task<List<FileDetail>> GetFilesWithHashes(string url)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(url);

            return JsonConvert.DeserializeObject<List<FileDetail>>(response);
        } catch (Exception e)
        {
            MainWindow.LogException(e); // Log the exception to understand what's going wrong

            throw; // Re-throw the exception to handle it further up the call stack
        }
    }

    public async Task<List<GameUpdate>> LoadGameUpdatesAsync(string url)
    {
        var json = await _httpClient.GetStringAsync(url);

        return JsonConvert.DeserializeObject<List<GameUpdate>>(json);
    }
}