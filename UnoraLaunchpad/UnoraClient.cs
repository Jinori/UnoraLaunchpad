using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Polly;
using Polly.Timeout;
using UnoraLaunchpad.Definitions;

namespace UnoraLaunchpad;

public sealed class UnoraClient
{
    private static HttpClient ApiClient;
    private static AsyncPolicy ResiliencePolicy;

    public UnoraClient()
    {
        var retryPolicy = Policy.Handle<HttpRequestException>()
                                .Or<TaskCanceledException>() // This will cover timeouts
                                .Or<TimeoutRejectedException>() // Explicitly handle Polly-induced timeouts
                                .WaitAndRetryAsync(5, attempt => TimeSpan.FromSeconds(attempt));

        ResiliencePolicy = retryPolicy;

        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        
        ApiClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(CONSTANTS.BASE_API_URL),
            Timeout = TimeSpan.FromMinutes(30)
        };
        
    }

    public async Task<string> GetLauncherVersionAsync()
    {
        try
        {
            var response = await ApiClient.GetStringAsync(CONSTANTS.GET_LAUNCHER_VERSION_RESOURCE);
            dynamic obj = JsonConvert.DeserializeObject(response);
            return (string)obj.Version;
        }
        catch (Exception ex)
        {
            // LOG THIS
            Console.WriteLine($"Failed to get launcher version: {ex}");
            throw;
        }
    }

    
    public Task<List<FileDetail>> GetFileDetailsAsync()
    {
        return ResiliencePolicy.ExecuteAsync(InnerGetFileDetailsAsync);

        static async Task<List<FileDetail>> InnerGetFileDetailsAsync()
        {
            var json = await ApiClient.GetStringAsync(CONSTANTS.GET_FILE_DETAILS_RESOURCE);

            return JsonConvert.DeserializeObject<List<FileDetail>>(json);
        }
    }

    public static async Task DownloadFileAsync(string relativePath, string destinationPath, IProgress<DownloadProgress> progress = null)
    {
        await ResiliencePolicy.ExecuteAsync(InnerGetFileAsync);

        async Task InnerGetFileAsync()
        {
            var resource = Uri.EscapeUriString(relativePath);

            if (File.Exists(destinationPath))
                File.Delete(destinationPath);

            using var response = await ApiClient.GetAsync($"get/{resource}", HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var totalRead = 0L;
            const int BUFFER_SIZE = 81920;
            var buffer = new byte[BUFFER_SIZE];

            using var networkStream = await response.Content.ReadAsStreamAsync();

            using var fileStream = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BUFFER_SIZE,
                true);

            var sw = Stopwatch.StartNew();
            var lastBytes = 0L;
            var lastTime = 0L;

            while (true)
            {
                var read = await networkStream.ReadAsync(buffer, 0, buffer.Length);

                if (read == 0)
                    break;

                await fileStream.WriteAsync(buffer, 0, read);
                totalRead += read;

                if ((progress != null) && (sw.ElapsedMilliseconds - lastTime > 500))
                {
                    var speed = (totalRead - lastBytes) / (double)(sw.ElapsedMilliseconds - lastTime) * 1000; // bytes/sec
                    lastBytes = totalRead;
                    lastTime = sw.ElapsedMilliseconds;

                    progress.Report(
                        new DownloadProgress
                        {
                            BytesReceived = totalRead,
                            TotalBytes = totalBytes,
                            SpeedBytesPerSec = speed
                        });
                }
            }

            // Final update to ensure UI reflects completion
            progress?.Report(
                new DownloadProgress
                {
                    BytesReceived = totalRead,
                    TotalBytes = totalBytes,
                    SpeedBytesPerSec = 0
                });
        }
    }


    // Helper class for reporting progress
    public sealed class DownloadProgress
    {
        public long BytesReceived { get; set; }
        public long TotalBytes { get; set; }
        public double SpeedBytesPerSec { get; set; }
    }


    public Task<List<GameUpdate>> GetGameUpdatesAsync()
    {
        return ResiliencePolicy.ExecuteAsync(InnerGetGameUpdatesAsync);

        static async Task<List<GameUpdate>> InnerGetGameUpdatesAsync()
        {
            var json = await ApiClient.GetStringAsync(CONSTANTS.GET_GAME_UPDATES_RESOURCE);

            return JsonConvert.DeserializeObject<List<GameUpdate>>(json);
        }
    }
}