using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
// System.Security.Cryptography is no longer needed here
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.ComponentModel; // Added for CancelEventArgs
using UnoraLaunchpad.Definitions;
using UnoraLaunchpad.Models; // For Settings, GameUpdate, FileDetail, GameApiRoutes
using UnoraLaunchpad.Utils; // Added for LoggingService, RelayCommand
using UnoraLaunchpad.Services;
using UnoraLaunchpad.Interfaces; // Required for service interfaces
using Application = System.Windows.Application;
// Using MessageBox alias removed as _userNotifierService should be used.

namespace UnoraLaunchpad
{
    public sealed partial class MainWindow
    {
        // LauncherSettingsPath is now in SettingsService
        // _launcherSettings is now managed by SettingsService

        private readonly FileService _fileService = new(); // FileService might not have an interface yet
        private readonly UnoraClient _unoraClient = new(); // UnoraClient might not have an interface yet
        private readonly IUserNotifierService _userNotifierService;
        private readonly ISettingsService _settingsService;
        private readonly IUpdateService _updateService;
        private readonly ILaunchService _launchService;
        private readonly INavigationService _navigationService;

        public ObservableCollection<GameUpdate> GameUpdates { get; } = new();
        public bool SkipIntro { get; set; }
        public bool UseDawndWindower { get; set; }
        public bool UseLocalhost { get; set; }
        public ICommand OpenGameUpdateCommand { get; }
        public object Sync { get; } = new();

        private void DiscordButton_Click(object sender, RoutedEventArgs e) =>
            // Replace with your Discord invite link
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://discord.gg/WkqbMVvDJq",
                UseShellExecute = true
            });

        public MainWindow()
        {
            InitializeComponent();

            // Instantiate concrete services and assign to interface variables
            // FileService and UnoraClient are kept concrete for now as they don't have interfaces defined in this step
            _userNotifierService = new UserNotifierService();
            _settingsService = new SettingsService(_fileService);
            // For services that depend on other services, pass the interface-typed fields:
            _updateService = new UpdateService(_unoraClient, _settingsService, _userNotifierService);
            _launchService = new LaunchService(_settingsService, _userNotifierService);
            _navigationService = new NavigationService();

            InitializeTrayIcon();
            OpenGameUpdateCommand = new RelayCommand<GameUpdate>(OpenGameUpdate); // Assumes RelayCommand is available
            DataContext = this;
            AssignUpdateServiceCallbacks();
        }

        private void AssignUpdateServiceCallbacks()
        {
            _updateService.PrepareProgressBarAction = PrepareProgressBar;
            _updateService.PrepareFileProgressAction = PrepareFileProgress;
            _updateService.UpdateFileProgressAction = UpdateFileProgress;
            _updateService.SetUiStateIdleAction = SetUiStateIdle;
        }

        /// <summary>
        /// Loads and applies launcher settings using SettingsService.
        /// </summary>
        public void ApplySettings()
        {
            var currentSettings = _settingsService.GetCurrentSettings();

            UseDawndWindower = currentSettings.UseDawndWindower;
            UseLocalhost = currentSettings.UseLocalhost;
            SkipIntro = currentSettings.SkipIntro;

            _settingsService.LoadAndApplyTheme(uri => App.ChangeTheme(uri));

            _settingsService.LoadAndApplyWindowDimensions((w, h, l, t) =>
            {
                // Check if dimensions are valid before applying to avoid collapsing the window
                // This check is now effectively handled within LoadAndApplyWindowDimensions itself.
                Width = w;
                Height = h;

                // Only apply Left and Top if they are not the default (0,0) or if explicitly set
                // This logic is also now better handled inside SettingsService to ensure on-screen placement
                if (l != 0 || t != 0 || (w == SystemParameters.PrimaryScreenWidth && h == SystemParameters.PrimaryScreenHeight)) // Handles maximized case or specific positions
                {
                    Left = l;
                    Top = t;
                }
            });
        }


        private void PatchNotesButton_Click(object sender, RoutedEventArgs e)
        {
            _navigationService.ShowPatchNotes(this);
        }

        // CheckAndUpdateLauncherAsync is now in UpdateService
        // GetLocalLauncherVersion is now in UpdateService
        // CheckForFileUpdates is now in UpdateService (will be CheckForFileUpdatesAsync)
        // NeedsUpdate is now in UpdateService
        // GetFilePath is now in UpdateService or internal to it
        // EnsureDirectoryExists is now in UpdateService or internal to it
        
        // ConfirmUpdateProceed remains here as it's UI specific with UpdateLockWindow
        private bool ConfirmUpdateProceed()
        {
            var currentSettings = _settingsService.GetCurrentSettings();
            if (currentSettings.UseLocalhost)
            {
                return true; // Skip showing the window if UseLocalhost is true
            }
            var lockWindow = new UpdateLockWindow();
            var result = lockWindow.ShowDialog();

            if (lockWindow.UserSkippedClosingClients) // Added this block
            {
                _userNotifierService.ShowWarning(
                    "You've chosen to skip closing active game clients. Game files may not update correctly, and you might encounter incorrect assets in-game until all clients are closed and the launcher performs a full update check.",
                    "Update Warning");
            }

            return result == true;
        }

        #region Progress UI Helpers

        private void SetUiStateUpdating() =>
            Dispatcher.Invoke(() =>
            {
                DownloadProgressPanel.Visibility = Visibility.Visible;
                StatusLabel.Visibility = Visibility.Collapsed;
                LaunchBtn.IsEnabled = false;
                ProgressFileName.Text = string.Empty;
                ProgressBytes.Text = "Checking for updates...";
                ProgressSpeed.Text = string.Empty;
                DownloadProgressBar.IsIndeterminate = true;
            });

        private void SetUiStateIdle() => Dispatcher.Invoke(() => LaunchBtn.IsEnabled = true);

        private void SetUiStateComplete()
        {
            DownloadProgressPanel.Visibility = Visibility.Collapsed;
            StatusLabel.Text = "Update complete.";
            StatusLabel.Visibility = Visibility.Visible;
            LaunchBtn.IsEnabled = true;
        }

        private void PrepareProgressBar(long totalBytesToDownload) =>
            Dispatcher.Invoke(() =>
            {
                ProgressFileName.Text = string.Empty;
                ProgressBytes.Text = "Applying updates...";
                ProgressSpeed.Text = string.Empty;
                DownloadProgressBar.IsIndeterminate = false;
                DownloadProgressBar.Minimum = 0;
                DownloadProgressBar.Maximum = totalBytesToDownload > 0 ? totalBytesToDownload : 1;
            });

        private void PrepareFileProgress(string fileName, long downloaded, long total) =>
            Dispatcher.Invoke(() =>
            {
                ProgressFileName.Text = fileName;
                ProgressBytes.Text = $"{FormattingHelper.FormatBytes(downloaded)} of {FormattingHelper.FormatBytes(total)}";
                ProgressSpeed.Text = string.Empty;
                DownloadProgressBar.Value = downloaded;
            });

        private void UpdateFileProgress(
            long bytesReceived,
            long totalDownloaded,
            long totalBytesToDownload,
            double speedBytesPerSec) =>
            Dispatcher.Invoke(() =>
            {
                ProgressBytes.Text = $"{FormattingHelper.FormatBytes(totalDownloaded + bytesReceived)} of {FormattingHelper.FormatBytes(totalBytesToDownload)}";
                ProgressSpeed.Text = $"@ {FormattingHelper.FormatSpeed(speedBytesPerSec)}";
                DownloadProgressBar.Value = totalDownloaded + bytesReceived;
            });
        #endregion

        #region System Tray / UI Initialization

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void CogButton_Click(object sender, RoutedEventArgs e)
        {
            var currentSettings = _settingsService.GetCurrentSettings();
            // NavigationService's ShowSettings will handle creating and showing the window.
            // It will need the 'this' (MainWindow) reference for the SettingsWindow constructor as it is now.
            _navigationService.ShowSettings(this, currentSettings, SaveSettings);
        }
        
        private void InitializeTrayIcon()
        {
            // Create an icon from a resource
            var iconUri = new Uri("pack://application:,,,/UnoraLaunchpad;component/favicon.ico", UriKind.RelativeOrAbsolute);
            var iconStream = Application.GetResourceStream(iconUri)?.Stream;

            if (iconStream != null)
                NotifyIcon = new NotifyIcon
                {
                    Icon = new Icon(iconStream),
                    Visible = true
                };

            // Create a context menu for the tray icon
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Launch Client", null, Launch);
            contextMenu.Items.Add("Open Launcher", null, TrayMenu_Open_Click);
            contextMenu.Items.Add("Exit", null, TrayMenu_Exit_Click);

            NotifyIcon.ContextMenuStrip = contextMenu;
            NotifyIcon.DoubleClick += (_, _) => ShowWindow();
        }

        private void TrayMenu_Exit_Click(object sender, EventArgs e)
        {
            NotifyIcon.Dispose();
            Application.Current.Shutdown();
        }

        private void TrayMenu_Open_Click(object sender, EventArgs e) => ShowWindow();

        private void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;
        }

        #endregion

        #region Window/Launcher Logic

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
                Hide();

            base.OnStateChanged(e);
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        #endregion

        #region Launcher Core
        private async Task PerformUpdateChecksAsync(string cancellationMessage = "Update cancelled by user.")
        {
            SetUiStateUpdating();
            if (ConfirmUpdateProceed())
            {
                await _updateService.CheckForFileUpdatesAsync();
            }
            else
            {
                _userNotifierService.ShowMessage(cancellationMessage, "Update Cancelled");
                SetUiStateIdle();
            }
        }
        public async void ReloadSettingsAndRefresh()
        {
            ApplySettings();           // Load settings from disk (SelectedGame, etc.)
            SetWindowTitle();          // Update the window title everywhere
            await LoadAndBindGameUpdates(); // Reload news/patches for the selected server

            await PerformUpdateChecksAsync();

            // SetUiStateComplete is called by the service or after await / or when idle is set.
            // The UpdateService calls SetUiStateIdleAction at the end of CheckForFileUpdatesAsync.
            // If an update happens, UI will go idle. If no updates or cancelled, it's also idle.
            // SetUiStateComplete might be more appropriate if PerformUpdateChecksAsync itself returned a status
            // indicating completion rather than just cancellation/idling.
            // For now, let's assume SetUiStateIdleAction from UpdateService is sufficient,
            // or we call SetUiStateComplete if no specific "complete" action is taken by the service.
            // Since UpdateService calls SetUiStateIdleAction, we might not need SetUiStateComplete here if idle implies completion of attempt.
            // However, if an update *was* performed, a "Complete" message is better.
            // Let's assume for now the final Dispatcher.BeginInvoke covers this.
            Dispatcher.BeginInvoke(new Action(SetUiStateComplete));
        }
        public void ReloadSettingsAndRefreshLocal()
        {
            ApplySettings(); // Reloads from disk into _launcherSettings
            // Optionally: Re-fetch game updates and other game-specific info
            _ = LoadAndBindGameUpdates();
        }

        // GetGameLaunchInfo was here but logic moved to LaunchService. Removing dead code.

        // Launch, GetServerConnection, ResolveHostname, PatchClient, InjectDll, RenameGameWindowAsync
        // are all moved to LaunchService.
        // The Launch button click handler will now call LaunchService.LaunchGameAsync()

        private async void Launch(object sender, EventArgs e) // Changed to async void
        {
            await _launchService.LaunchGameAsync();
        }

        private void SetWindowTitle()
        {
            var currentSettings = _settingsService.GetCurrentSettings();
            var selectedGame = currentSettings?.SelectedGame?.Trim() ?? "Unora";
            var title = selectedGame switch
            {
                "Legends" => "Legends: Age of Chaos",
                "Unora"   => "Unora: Elemental Harmony",
                _         => $"Unora Launcher" // Default title if game not recognized
            };

            Title = title; // OS-level window title
            WindowTitleLabel.Content = title; // Custom title bar label
        }



        #endregion

        #region Game Updates

        private async void Launcher_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplySettings();
                SetWindowTitle();
                await LoadAndBindGameUpdates();

                // Use the new helper method, potentially with a different cancellation message if needed for startup
                await PerformUpdateChecksAsync("Initial update check cancelled by user.");

                await _updateService.CheckAndUpdateLauncherAsync(() =>
                {
                    Application.Current.Shutdown();
                    // Environment.Exit(0); // Generally not needed if Application.Current.Shutdown() is effective
                });
            }
            catch (Exception ex)
            {
                LoggingService.LogException(ex);
                // User-facing message should be more generic. Detailed error is in the log.
                _userNotifierService.ShowError("A critical error occurred during startup. Please check the logs for more details or contact support.", "Startup Error");
            }
            finally
            {
                // SetUiStateComplete is called by the service or after await.
                // Similar to ReloadSettingsAndRefresh, relying on service callbacks or explicit call here.
                Dispatcher.BeginInvoke(new Action(SetUiStateComplete));
            }
        }

        // GetCurrentApiRoutes for general game updates (not file updates) can remain if needed,
        // or be part of another service (e.g. GameDataService).
        // For now, keeping the local GetCurrentApiRoutes for LoadAndBindGameUpdates.
        private GameApiRoutes GetCurrentApiRoutesForGameUpdates() // Renamed for clarity
        {
            var currentSettings = _settingsService.GetCurrentSettings();
            var baseUrl = CONSTANTS.BASE_API_URL.TrimEnd('/');
            var selectedGame = string.IsNullOrWhiteSpace(currentSettings?.SelectedGame)
                ? CONSTANTS.UNORA_FOLDER_NAME
                : currentSettings.SelectedGame;
            return new GameApiRoutes(baseUrl, selectedGame);
        }
        
        public async Task LoadAndBindGameUpdates()
        {
            var apiRoutes = GetCurrentApiRoutesForGameUpdates(); // Use the renamed method
            var gameUpdates = await _unoraClient.GetGameUpdatesAsync(apiRoutes.GameUpdates);
            GameUpdatesControl.DataContext = new { GameUpdates = gameUpdates };
        }

        private void OpenGameUpdate(GameUpdate gameUpdate)
        {
            _navigationService.ShowGameUpdateDetail(gameUpdate, this);
        }

        #endregion

        public void SaveSettings(Settings settings)
        {
            _settingsService.SaveSettings(settings);
            // Refresh local properties from the authoritative source (SettingsService)
            var currentSettings = _settingsService.GetCurrentSettings();
            UseDawndWindower = currentSettings.UseDawndWindower;
            UseLocalhost = currentSettings.UseLocalhost;
            SkipIntro = currentSettings.SkipIntro;
            // Theme is applied via ApplySettings -> LoadAndApplyTheme
            // Window dimensions are saved on closing
        }
        
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // SettingsService handles null checks and saving logic internally
            _settingsService.UpdateAndSaveWindowBounds(ActualWidth, ActualHeight, Left, Top, WindowState);
            NotifyIcon?.Dispose(); // Dispose the tray icon when the main window is closing
        }
    }
}