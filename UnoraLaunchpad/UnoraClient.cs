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
using UnoraLaunchpad.Utils; // For LoggingService if used

namespace UnoraLaunchpad;

/// <summary>
/// Provides methods for interacting with the Unora game and update servers.
/// This client handles API requests for launcher version, game file details,
/// file downloads, and game updates, incorporating resilience policies like retries.
/// </summary>
public sealed class UnoraClient
{
    private static readonly HttpClient ApiClient; // Made readonly
    private static readonly AsyncPolicy ResiliencePolicy; // Made readonly

    /// <summary>
    /// Initializes static members of the <see cref="UnoraClient"/> class.
    /// Configures the static <see cref="HttpClient"/> and resilience policy.
    /// </summary>
    static UnoraClient()
    {
        var retryPolicy = Policy.Handle<HttpRequestException>()
                                .Or<TaskCanceledException>() // This will cover timeouts from HttpClient
                                .Or<TimeoutRejectedException>() // Explicitly handle Polly-induced timeouts
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
            // BaseAddress can be set per request if it varies, or here if truly static for all calls.
            // Example: BaseAddress = new Uri(CONSTANTS.BASE_API_URL),
            Timeout = TimeSpan.FromMinutes(30)
        };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnoraClient"/> class.
    /// (Instance constructor can be empty if all setup is static, or used for instance-specific dependencies if any were added later)
    /// </summary>
    public UnoraClient()
    {
        // Instance-specific initialization, if any, would go here.
        // For now, it's empty as ApiClient and ResiliencePolicy are static.
    }

    /// <summary>
    /// Asynchronously retrieves the latest launcher version from the server.
    /// </summary>
    /// <returns>A string representing the latest launcher version.</returns>
    /// <exception cref="HttpRequestException">Thrown if the HTTP request fails.</exception>
    /// <exception cref="JsonException">Thrown if deserialization of the server response fails.</exception>
    /// <remarks>Uses the <see cref="CONSTANTS.GET_LAUNCHER_VERSION_RESOURCE"/> endpoint relative to <see cref="CONSTANTS.BASE_API_URL"/>.</remarks>
    public async Task<string> GetLauncherVersionAsync()
    {
        try
        {
            // Ensure ApiClient.BaseAddress is correctly set if not done in constructor or if it can change
            if (ApiClient.BaseAddress == null) ApiClient.BaseAddress = new Uri(CONSTANTS.BASE_API_URL);

            var response = await ApiClient.GetStringAsync(CONSTANTS.GET_LAUNCHER_VERSION_RESOURCE);
            dynamic obj = JsonConvert.DeserializeObject(response);
            return (string)obj.Version;
        }
        catch (Exception ex)
        {
            // Debug.WriteLine($"[UnoraClient] Failed to get launcher version: {ex}"); // Replaced by more specific logging
            LoggingService.LogException(ex); // Log the full exception
            throw; // Re-throw to allow caller to handle
        }
    }

    /// <summary>
    /// Asynchronously retrieves a list of game file details from the specified URL.
    /// Applies a resilience policy to handle transient network errors.
    /// </summary>
    /// <param name="fileDetailsUrl">The absolute URL from which to fetch the file details JSON.</param>
    /// <returns>A list of <see cref="FileDetail"/> objects representing the game files.</returns>
    /// <exception cref="HttpRequestException">Thrown if the HTTP request fails after retries.</exception>
    /// <exception cref="JsonException">Thrown if deserialization of the server response fails.</exception>
    public Task<List<FileDetail>> GetFileDetailsAsync(string fileDetailsUrl)
    {
        // Ensure URL is absolute or BaseAddress is set.
        // If fileDetailsUrl can be relative, ensure ApiClient.BaseAddress is set appropriately before this call.
        return ResiliencePolicy.ExecuteAsync(() => InnerGetFileDetailsAsync(fileDetailsUrl));

        static async Task<List<FileDetail>> InnerGetFileDetailsAsync(string url)
        {
            var json = await ApiClient.GetStringAsync(url); // Assumes url is absolute or BaseAddress is set
            return JsonConvert.DeserializeObject<List<FileDetail>>(json);
        }
    }

    /// <summary>
    /// Asynchronously downloads a file from the specified URL to a destination path, reporting progress.
    /// Applies a resilience policy. Ensures the destination directory exists. Deletes the file if it already exists.
    /// </summary>
    /// <param name="fileDownloadUrl">The absolute URL from which to download the file.</param>
    /// <param name="destinationPath">The local path where the downloaded file will be saved.</param>
    /// <param name="progress">An optional <see cref="IProgress{T}"/> instance to report download progress.</param>
    /// <returns>A task representing the asynchronous download operation.</returns>
    /// <exception cref="HttpRequestException">Thrown if the HTTP request fails after retries.</exception>
    /// <exception cref="IOException">Thrown if an I/O error occurs (e.g., creating directory, writing file).</exception>
    public async Task DownloadFileAsync(string fileDownloadUrl, string destinationPath, IProgress<DownloadProgress> progress = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Path.GetDirectoryName(destinationPath)))
            {
                // Log this specific argument error before throwing
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
            const int BUFFER_SIZE = 81920; // 80KB buffer
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
                    // Report progress roughly every 500ms or if significant data downloaded
                    if (currentTimeTicks - lastReportedTimeTicks > TimeSpan.FromMilliseconds(500).Ticks || totalRead == totalBytes)
                    {
                        double speed = 0;
                        if (currentTimeTicks - lastReportedTimeTicks > 0) // Avoid division by zero
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
            // Final progress report
            progress?.Report(new DownloadProgress { BytesReceived = totalRead, TotalBytes = totalBytes, SpeedBytesPerSec = 0 });
            });
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Unhandled error during DownloadFileAsync for URL '{fileDownloadUrl}' to '{destinationPath}'. Error: {ex.Message}", ex);
            throw; // Re-throw to allow caller to handle
        }
    }

    /// <summary>
    /// Represents the progress of a file download operation.
    /// </summary>
    public sealed class DownloadProgress
    {
        /// <summary>
        /// Gets or sets the number of bytes received so far.
        /// </summary>
        public long BytesReceived { get; set; }
        /// <summary>
        /// Gets or sets the total number of bytes expected for the download.
        /// May be -1 if the total size is unknown.
        /// </summary>
        public long TotalBytes { get; set; }
        /// <summary>
        /// Gets or sets the current download speed in bytes per second.
        /// </summary>
        public double SpeedBytesPerSec { get; set; }
    }

    /// <summary>
    /// Asynchronously retrieves a list of game updates from the specified URL.
    /// Applies a resilience policy to handle transient network errors.
    /// </summary>
    /// <param name="gameUpdatesUrl">The absolute URL from which to fetch the game updates JSON.</param>
    /// <returns>A list of <see cref="GameUpdate"/> objects.</returns>
    /// <exception cref="HttpRequestException">Thrown if the HTTP request fails after retries.</exception>
    /// <exception cref="JsonException">Thrown if deserialization of the server response fails.</exception>
    public Task<List<GameUpdate>> GetGameUpdatesAsync(string gameUpdatesUrl)
    {
        return ResiliencePolicy.ExecuteAsync(() => InnerGetGameUpdatesAsync(gameUpdatesUrl));

        static async Task<List<GameUpdate>> InnerGetGameUpdatesAsync(string url)
        {
            // Ensure ApiClient.BaseAddress is set if url is relative, or url is absolute
            var json = await ApiClient.GetStringAsync(url);
            return JsonConvert.DeserializeObject<List<GameUpdate>>(json);
        }
    }
}