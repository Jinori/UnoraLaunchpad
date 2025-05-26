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

    
    public Task<List<FileDetail>> GetFileDetailsAsync(string fileDetailsUrl)
    {
        return ResiliencePolicy.ExecuteAsync(() => InnerGetFileDetailsAsync(fileDetailsUrl));

        static async Task<List<FileDetail>> InnerGetFileDetailsAsync(string url)
        {
            var json = await ApiClient.GetStringAsync(url);
            return JsonConvert.DeserializeObject<List<FileDetail>>(json);
        }
    }



    public async Task DownloadFileAsync(string fileDownloadUrl, string destinationPath, IProgress<DownloadProgress> progress = null)
    {
        await ResiliencePolicy.ExecuteAsync(() => InnerGetFileAsync(fileDownloadUrl, destinationPath, progress));

        static async Task InnerGetFileAsync(string url, string destinationPath, IProgress<DownloadProgress> progress)
        {
            if (File.Exists(destinationPath))
                File.Delete(destinationPath);

            using var response = await ApiClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var totalRead = 0L;
            const int BUFFER_SIZE = 81920;
            var buffer = new byte[BUFFER_SIZE];

            using var networkStream = await response.Content.ReadAsStreamAsync();

            // === ENSURE DIRECTORY EXISTS HERE ===
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

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


    public Task<List<GameUpdate>> GetGameUpdatesAsync(string gameUpdatesUrl)
    {
        return ResiliencePolicy.ExecuteAsync(() => InnerGetGameUpdatesAsync(gameUpdatesUrl));

        static async Task<List<GameUpdate>> InnerGetGameUpdatesAsync(string url)
        {
            var json = await ApiClient.GetStringAsync(url);
            return JsonConvert.DeserializeObject<List<GameUpdate>>(json);
        }
    }
}