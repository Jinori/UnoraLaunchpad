using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
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
            BaseAddress = new Uri(CONSTANTS.BASE_API_URL),
            Timeout = TimeSpan.FromMinutes(30)
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
        
        async Task InnerGetFileAsync()
        {
            var resource = Uri.EscapeUriString(relativePath);
            
            if (File.Exists(destinationPath))
                File.Delete(destinationPath);

            using var response = await ApiClient.GetAsync($"get/{resource}", HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var networkStream = await response.Content.ReadAsStreamAsync();

            using var fileStream = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                true);

            await networkStream.CopyToAsync(fileStream);
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