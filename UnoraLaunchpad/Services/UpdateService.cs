using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnoraLaunchpad.Definitions;
using UnoraLaunchpad.Models;    // Required for Settings, FileDetail, GameApiRoutes
using UnoraLaunchpad.Interfaces; // Required for IUpdateService
using UnoraLaunchpad.Utils; // For FileHashHelper

namespace UnoraLaunchpad.Services
{
    /// <summary>
    /// Handles checking for and applying updates for both the launcher itself and game files.
    /// It coordinates with <see cref="UnoraClient"/> for server communication,
    /// <see cref="SettingsService"/> (or its interface <see cref="ISettingsService"/>) for game-specific settings,
    /// and <see cref="UserNotifierService"/> (or <see cref="IUserNotifierService"/>) for user interaction.
    /// UI updates during the process are handled via callback actions.
    /// </summary>
    public class UpdateService : IUpdateService
    {
        private readonly UnoraClient _unoraClient;
        private readonly ISettingsService _settingsService;
        private readonly IUserNotifierService _userNotifierService;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateService"/> class.
        /// </summary>
        /// <param name="unoraClient">The client for communicating with Unora game/update servers.</param>
        /// <param name="settingsService">The service for accessing launcher and game settings.</param>
        /// <param name="userNotifierService">The service for notifying the user of events or asking for confirmation.</param>
        /// <exception cref="ArgumentNullException">Thrown if any of the service dependencies are null.</exception>
        public UpdateService(UnoraClient unoraClient, ISettingsService settingsService, IUserNotifierService userNotifierService)
        {
            _unoraClient = unoraClient ?? throw new ArgumentNullException(nameof(unoraClient));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _userNotifierService = userNotifierService ?? throw new ArgumentNullException(nameof(userNotifierService));
        }

        /// <summary>
        /// Gets the current version of the running launcher executable.
        /// </summary>
        /// <returns>A string representing the launcher's file version, or "0" if not found.</returns>
        public string GetLocalLauncherVersion()
        {
            var exePath = Process.GetCurrentProcess().MainModule!.FileName!;
            return FileVersionInfo.GetVersionInfo(exePath).FileVersion ?? "0";
        }

        /// <summary>
        /// Checks if the launcher itself needs an update by comparing local version with server version.
        /// If an update is needed, it launches the bootstrapper to perform the update and then invokes a shutdown action.
        /// </summary>
        /// <param name="shutdownApplication">An action to call to shut down the current application instance if an update is initiated.</param>
        /// <remarks>
        /// This check is typically performed only for the "Unora" game/client, as per original logic.
        /// </remarks>
        public async Task CheckAndUpdateLauncherAsync(Action shutdownApplication)
        {
            var currentSettings = _settingsService.GetCurrentSettings();
            var selectedGame = currentSettings?.SelectedGame ?? "Unora"; // Default to Unora if not set
            if (!selectedGame.Equals("Unora", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                var serverVersion = await _unoraClient.GetLauncherVersionAsync();
                var localVersion = GetLocalLauncherVersion();

                if (serverVersion != localVersion)
                {
                    var bootstrapperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Unora\\UnoraBootstrapper.exe"); // Consider making "Unora" part of a constant or config
                    var currentLauncherPath = Process.GetCurrentProcess().MainModule!.FileName!;

                    var psi = new ProcessStartInfo
                    {
                        FileName = bootstrapperPath,
                        Arguments = $"\"{currentLauncherPath}\" {Process.GetCurrentProcess().Id}",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    shutdownApplication?.Invoke();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to check for launcher updates or start bootstrapper. Game: {selectedGame}. Error: {ex.Message}", ex);
                // Optionally notify user, but this is often a background task. For now, just log.
            }
        }
            {
                var bootstrapperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Unora\\UnoraBootstrapper.exe"); // Consider making "Unora" part of a constant or config
                var currentLauncherPath = Process.GetCurrentProcess().MainModule!.FileName!;

                var psi = new ProcessStartInfo
                {
                    FileName = bootstrapperPath,
                    Arguments = $"\"{currentLauncherPath}\" {Process.GetCurrentProcess().Id}",
                    UseShellExecute = true
                };
                Process.Start(psi);
                shutdownApplication?.Invoke();
            }
        }

        /// <summary>
        /// Determines the appropriate API routes based on the currently selected game in settings.
        /// </summary>
        /// <returns>A <see cref="GameApiRoutes"/> object configured for the selected game.</returns>
        private GameApiRoutes GetCurrentApiRoutes()
        {
            var currentSettings = _settingsService.GetCurrentSettings();
            var baseUrl = CONSTANTS.BASE_API_URL.TrimEnd('/');
            var selectedGame = string.IsNullOrWhiteSpace(currentSettings?.SelectedGame)
                ? CONSTANTS.UNORA_FOLDER_NAME // Default to "Unora" if not set or empty
                : currentSettings.SelectedGame;
            return new GameApiRoutes(baseUrl, selectedGame);
        }

        /// <summary>
        /// Constructs the full local file path for a given relative path, based on the selected game's directory.
        /// </summary>
        /// <param name="relativePath">The relative path of the file within the game's folder structure.</param>
        /// <returns>The full local file path.</returns>
        public string GetFilePath(string relativePath)
        {
            var currentSettings = _settingsService.GetCurrentSettings();
            var gameFolder = currentSettings?.SelectedGame ?? CONSTANTS.UNORA_FOLDER_NAME;
            return Path.Combine(gameFolder, relativePath);
        }

        /// <summary>
        /// Ensures that the directory for the given file path exists. If not, it creates the directory.
        /// </summary>
        /// <param name="filePath">The full path to the file whose directory needs to exist.</param>
        public void EnsureDirectoryExists(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath)!;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// Determines if a game file needs to be updated by comparing its local hash with the server-provided hash.
        /// A file also needs an update if it doesn't exist locally.
        /// </summary>
        /// <param name="fileDetail">The <see cref="FileDetail"/> object containing server-side hash and relative path.</param>
        /// <returns><c>true</c> if the file needs an update; otherwise, <c>false</c>.</returns>
        private bool NeedsUpdate(FileDetail fileDetail)
        {
            var filePath = GetFilePath(fileDetail.RelativePath);
            if (!File.Exists(filePath))
                return true;
            return !FileHashHelper.CalculateHash(filePath).Equals(fileDetail.Hash, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets or sets an action to prepare the main progress bar in the UI. Parameter is total bytes to download.
        /// </summary>
        public Action<long> PrepareProgressBarAction { get; set; }
        /// <summary>
        /// Gets or sets an action to update UI for individual file progress. Parameters are: file name, current total downloaded, overall total bytes.
        /// </summary>
        public Action<string, long, long> PrepareFileProgressAction { get; set; }
        /// <summary>
        /// Gets or sets an action to update UI file progress. Parameters are: bytes received for current file, overall total downloaded, overall total to download, download speed.
        /// </summary>
        public Action<long, long, long, double> UpdateFileProgressAction { get; set; }
        /// <summary>
        /// Gets or sets an action to set the UI state to idle (e.g., after updates are complete or cancelled).
        /// </summary>
        public Action SetUiStateIdleAction { get; set; }

        /// <summary>
        /// Checks for game file updates based on the current game selection.
        /// Downloads necessary files and reports progress through UI callbacks.
        /// </summary>
        /// <remarks>
        /// If not running with localhost settings, this method will confirm with the user before starting downloads.
        /// UI state management is expected to be handled by the calling context (MainWindow)
        /// before and after this method, although SetUiStateIdleAction is invoked on cancellation or if no updates are needed.
        /// </remarks>
        public async Task CheckForFileUpdatesAsync()
        {
            GameApiRoutes apiRoutes = GetCurrentApiRoutes();
            List<FileDetail> fileDetails;
            try
            {
                fileDetails = await _unoraClient.GetFileDetailsAsync(apiRoutes.GameDetails);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to get file details from {apiRoutes.GameDetails}. Error: {ex.Message}", ex);
                _userNotifierService.ShowError("Failed to retrieve file list for updates. Please check your connection and try again.", "Update Error");
                SetUiStateIdleAction?.Invoke();
                return;
            }

            Debug.WriteLine($"[UpdateService] Checking {fileDetails.Count} files for game specified by {apiRoutes.GameDetails}");

            var filesToUpdate = fileDetails.Where(NeedsUpdate).ToList();
            Debug.WriteLine($"[UpdateService] Files to update: {filesToUpdate.Count}");

            if (!filesToUpdate.Any())
            {
                 Debug.WriteLine($"[UpdateService] No files to update.");
                 SetUiStateIdleAction?.Invoke();
                 return;
            }

            var totalBytesToDownload = filesToUpdate.Sum(f => f.Size);

            var currentSettings = _settingsService.GetCurrentSettings();
            if (!currentSettings.UseLocalhost)
            {
                 if (!_userNotifierService.Confirm("Updates are available. Proceed with download?", "Unora Launcher"))
                 {
                    _userNotifierService.ShowMessage("Update cancelled. Please update later.", "Unora Launcher");
                    SetUiStateIdleAction?.Invoke();
                    return;
                 }
            }

            PrepareProgressBarAction?.Invoke(totalBytesToDownload);
            var totalDownloadedAggregated = 0L;

            foreach (var fileDetail in filesToUpdate)
            {
                Debug.WriteLine($"[UpdateService] Downloading file: {fileDetail.RelativePath}");
                PrepareFileProgressAction?.Invoke(fileDetail.RelativePath, totalDownloadedAggregated, totalBytesToDownload);

                var filePath = GetFilePath(fileDetail.RelativePath);
                EnsureDirectoryExists(filePath);

                long currentFileBytesDownloaded = 0;
                var progress = new Progress<UnoraClient.DownloadProgress>(p =>
                {
                    currentFileBytesDownloaded = p.BytesReceived;
                    UpdateFileProgressAction?.Invoke(currentFileBytesDownloaded, totalDownloadedAggregated, totalBytesToDownload, p.SpeedBytesPerSec);
                });

                try
                {
                    await _unoraClient.DownloadFileAsync(apiRoutes.GameFile(fileDetail.RelativePath), filePath, progress);
                    totalDownloadedAggregated += fileDetail.Size;
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"Download failed for {fileDetail.RelativePath}. URL: {apiRoutes.GameFile(fileDetail.RelativePath)}. Error: {ex.Message}", ex);
                    _userNotifierService.ShowError($"Failed to download {fileDetail.RelativePath}. Please check logs for details.", "Update Error");
                    // Decide if one failed download should halt others. Currently, it continues.
                }
            }

            Debug.WriteLine($"[UpdateService] All update attempts completed. Total bytes downloaded (approx): {totalDownloadedAggregated}");
            SetUiStateIdleAction?.Invoke();
        }
    }
}
