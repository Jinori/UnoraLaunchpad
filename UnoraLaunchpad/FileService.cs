using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace UnoraLaunchpad;

public sealed class FileService
{
    public static async Task DownloadFileFromServer(
        string downloadUrl,
        string filePath,
        string savePath,
        IProgress<long> progress
    )
    {
        try
        {
            var directoryPath = Path.GetDirectoryName(savePath);

            if ((directoryPath != null) && !Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            using var client = new HttpClient();

            using var response = await client.GetAsync($"{downloadUrl}/{filePath}", HttpCompletionOption.ResponseHeadersRead);

            response.EnsureSuccessStatusCode();
            var totalSize = response.Content.Headers.ContentLength ?? 0;
            const int BLOCK_SIZE = 65536; // Larger buffer size
            const int REPORT_INTERVAL = 500000; // Report progress every 500 KB

            using var contentStream = await response.Content.ReadAsStreamAsync();

            using var fileStream = File.Create(savePath);

            var buffer = new byte[BLOCK_SIZE];
            int bytesRead;
            var totalBytesRead = 0L;
            var bytesReadSinceLastReport = 0L;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;
                bytesReadSinceLastReport += bytesRead;

                if (bytesReadSinceLastReport >= REPORT_INTERVAL)
                {
                    progress?.Report(totalBytesRead * 100 / totalSize);
                    bytesReadSinceLastReport = 0;
                }
            }

            // Report progress one last time to ensure 100% completion
            progress?.Report(100);
        } catch (Exception e)
        {
            MainWindow.LogException(e);
            Debug.WriteLine($"Error downloading and saving file {filePath}: {e.Message}");
        }
    }

    public Settings LoadSettings(string path)
    {
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);

            return JsonConvert.DeserializeObject<Settings>(json);
        }

        return new Settings();
    }

    public void SaveSettings(Settings settings, string path)
    {
        var directoryPath = Path.GetDirectoryName(path);

        if ((directoryPath != null) && !Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);

        var json = JsonConvert.SerializeObject(settings);
        File.WriteAllText(path, json);
    }
}