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
using UnoraLaunchpad.Models;    // For FileDetail, GameUpdate
using UnoraLaunchpad.Utils;     // For LoggingService

namespace UnoraLaunchpad.Services; // Updated namespace

/// <summary>
/// Provides methods for interacting with the Unora game and update servers.
/// This client handles API requests for launcher version, game file details,
/// file downloads, and game updates, incorporating resilience policies like retries.
/// </summary>
public sealed class UnoraClient
{
    private static readonly HttpClient ApiClient;
    private static readonly AsyncPolicy ResiliencePolicy;

    /// <summary>
    /// Initializes static members of the <see cref="UnoraClient"/> class.
    /// Configures the static <see cref="HttpClient"/> and resilience policy.
    /// </summary>
    static UnoraClient()
    {
        var retryPolicy = Policy.Handle<HttpRequestException>()
                                .Or<TaskCanceledException>()
                                .Or<TimeoutRejectedException>()
                                .WaitAndRetryAsync(5, attempt => TimeSpan.FromSeconds(Math.Pow(2,attempt)), (exception, timespan, attempt, context) =>
                                {
                                    Debug.WriteLine($"[UnoraClient] Request failed. Waiting {timespan.TotalSeconds}s before retry {attempt}/5. Error: {exception.Message}");
                                    LoggingService.LogWarning($"[UnoraClient] Request failed (attempt {attempt}/5). Error: {exception.Message}. Retrying in {timespan.TotalSeconds}s.");
                                });

        ResiliencePolicy = retryPolicy;

        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        ApiClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(30)
        };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnoraClient"/> class.
    /// </summary>
    public UnoraClient()
    {
    }

    /// <summary>
    /// Asynchronously retrieves the latest launcher version from the server.
    /// </summary>
    public async Task<string> GetLauncherVersionAsync()
    {
        try
        {
            if (ApiClient.BaseAddress == null) ApiClient.BaseAddress = new Uri(CONSTANTS.BASE_API_URL);

            var response = await ApiClient.GetStringAsync(CONSTANTS.GET_LAUNCHER_VERSION_RESOURCE);
            dynamic obj = JsonConvert.DeserializeObject(response);
            return (string)obj.Version;
        }
        catch (Exception ex)
        {
            LoggingService.LogException(ex);
            throw;
        }
    }

    /// <summary>
    /// Asynchronously retrieves a list of game file details from the specified URL.
    /// </summary>
    public Task<List<FileDetail>> GetFileDetailsAsync(string fileDetailsUrl)
    {
        return ResiliencePolicy.ExecuteAsync(() => InnerGetFileDetailsAsync(fileDetailsUrl));

        static async Task<List<FileDetail>> InnerGetFileDetailsAsync(string url)
        {
            var json = await ApiClient.GetStringAsync(url);
            return JsonConvert.DeserializeObject<List<FileDetail>>(json);
        }
    }

    /// <summary>
    /// Asynchronously downloads a file from the specified URL to a destination path, reporting progress.
    /// </summary>
    public async Task DownloadFileAsync(string fileDownloadUrl, string destinationPath, IProgress<DownloadProgress> progress = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Path.GetDirectoryName(destinationPath)))
            {
                LoggingService.LogError($"Destination path '{destinationPath}' for URL '{fileDownloadUrl}' is invalid: must include a directory.");
                throw new ArgumentException("Destination path must include a directory.", nameof(destinationPath));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            await ResiliencePolicy.ExecuteAsync(async () =>
            {
                using var response = await ApiClient.GetAsync(fileDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var totalRead = 0L;
                const int BUFFER_SIZE = 81920;
                var buffer = new byte[BUFFER_SIZE];

                using var networkStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, BUFFER_SIZE, true);

                var stopwatch = Stopwatch.StartNew();
                long lastReportedBytes = 0;
                long lastReportedTimeTicks = stopwatch.Elapsed.Ticks;

                int bytesRead;
                while ((bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (progress != null)
                    {
                        var currentTimeTicks = stopwatch.Elapsed.Ticks;
                        if (currentTimeTicks - lastReportedTimeTicks > TimeSpan.FromMilliseconds(500).Ticks || totalRead == totalBytes)
                        {
                            double speed = 0;
                            if (currentTimeTicks - lastReportedTimeTicks > 0)
                            {
                                 speed = (totalRead - lastReportedBytes) / TimeSpan.FromTicks(currentTimeTicks - lastReportedTimeTicks).TotalSeconds;
                            }

                            progress.Report(new DownloadProgress
                            {
                                BytesReceived = totalRead,
                                TotalBytes = totalBytes,
                                SpeedBytesPerSec = speed
                            });
                            lastReportedBytes = totalRead;
                            lastReportedTimeTicks = currentTimeTicks;
                        }
                    }
                }
                stopwatch.Stop();
                progress?.Report(new DownloadProgress { BytesReceived = totalRead, TotalBytes = totalBytes, SpeedBytesPerSec = 0 });
            });
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Unhandled error during DownloadFileAsync for URL '{fileDownloadUrl}' to '{destinationPath}'. Error: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// Represents the progress of a file download operation.
    /// </summary>
    public sealed class DownloadProgress
    {
        /// <summary>Gets or sets the number of bytes received so far.</summary>
        public long BytesReceived { get; set; }
        /// <summary>Gets or sets the total number of bytes expected for the download.</summary>
        public long TotalBytes { get; set; }
        /// <summary>Gets or sets the current download speed in bytes per second.</summary>
        public double SpeedBytesPerSec { get; set; }
    }

    /// <summary>
    /// Asynchronously retrieves a list of game updates from the specified URL.
    /// </summary>
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
