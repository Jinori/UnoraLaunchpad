using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Flurl.Http;
using Newtonsoft.Json;
using Polly;
using Polly.Timeout;
using UnoraLaunchpad.Definitions;

namespace UnoraLaunchpad;

public sealed class UnoraClient
{
    private readonly HttpClient ApiClient;
    private readonly AsyncPolicy ResiliencePolicy;

    public UnoraClient()
    {
        var retryPolicy = Policy.Handle<HttpRequestException>()
                                .Or<TaskCanceledException>() // This will cover timeouts
                                .Or<TimeoutRejectedException>() // Explicitly handle Polly-induced timeouts
                                .WaitAndRetryAsync(5, attempt => TimeSpan.FromSeconds(attempt));

        ResiliencePolicy = retryPolicy;
        
        ApiClient = new HttpClient
        {
            BaseAddress = new Uri(CONSTANTS.BASE_API_URL)
        };
    }

    public Task<List<FileDetail>> GetFileDetailsAsync()
    {
        return ResiliencePolicy.ExecuteAsync(InnerGetFileDetailsAsync);
        
        async Task<List<FileDetail>> InnerGetFileDetailsAsync()
        {
            var json = await ApiClient.GetStringAsync(CONSTANTS.GET_FILE_DETAILS_RESOURCE);

            return JsonConvert.DeserializeObject<List<FileDetail>>(json);
        }
    }

    public Task DownloadFileAsync(string relativePath, string destinationPath)
    {
        return ResiliencePolicy.ExecuteAsync(InnerGetFileAsync);
        
        Task InnerGetFileAsync()
        {
            var request = new FlurlRequest(ApiClient.BaseAddress + CONSTANTS.GET_FILE_RESOURCE + Uri.EscapeUriString(relativePath));
            var destinationFolder = Path.GetDirectoryName(destinationPath);
            var destinationFileName = Path.GetFileName(destinationPath);

            if (File.Exists(destinationPath))
                File.Delete(destinationPath);

            return request.WithTimeout(300)
                          .DownloadFileAsync(destinationFolder, destinationFileName, 1024 * 1024 * 5); //5mb buffer
        }
    }

    public Task<List<GameUpdate>> GetGameUpdatesAsync()
    {
        return ResiliencePolicy.ExecuteAsync(InnerGetGameUpdatesAsync);

        async Task<List<GameUpdate>> InnerGetGameUpdatesAsync()
        {
            var json = await ApiClient.GetStringAsync(CONSTANTS.GET_GAME_UPDATES_RESOURCE);

            return JsonConvert.DeserializeObject<List<GameUpdate>>(json);
        }
    }
}