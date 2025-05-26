using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using UnoraLaunchpad.Definitions;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace UnoraLaunchpad
{
    public sealed partial class MainWindow
    {
        private static readonly string LauncherSettingsPath = "LauncherSettings/settings.json";

        private readonly FileService FileService = new();
        private readonly UnoraClient UnoraClient = new();
        private Settings _launcherSettings; // Added field for settings
        private NotifyIcon NotifyIcon;

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
            InitializeTrayIcon();
            OpenGameUpdateCommand = new RelayCommand<GameUpdate>(OpenGameUpdate);
            DataContext = this;
        }

        /// <summary>
        /// Loads and applies launcher settings from disk.
        /// </summary>
        /// <summary>
        /// Loads and applies launcher settings from disk.
        /// </summary>
        public void ApplySettings()
        {
            _launcherSettings = FileService.LoadSettings(LauncherSettingsPath);
            if (_launcherSettings == null)
            {
                _launcherSettings = new Settings(); // Fallback to default settings if loading fails
            }

            UseDawndWindower = _launcherSettings.UseDawndWindower;
            UseLocalhost = _launcherSettings.UseLocalhost;
            SkipIntro = _launcherSettings.SkipIntro;

            var themeName = _launcherSettings.SelectedTheme;
            if (string.IsNullOrEmpty(themeName))
            {
                themeName = "Dark"; // Default theme
                _launcherSettings.SelectedTheme = themeName; // Ensure default is set in current settings object
            }

            // Map theme name to file URI
            Uri themeUri;
            switch (themeName)
            {
                case "Light":
                    themeUri = new Uri("pack://application:,,,/Resources/LightTheme.xaml", UriKind.Absolute);
                    break;
                case "Teal":
                    themeUri = new Uri("pack://application:,,,/Resources/TealTheme.xaml", UriKind.Absolute);
                    break;
                case "Violet":
                    themeUri = new Uri("pack://application:,,,/Resources/VioletTheme.xaml", UriKind.Absolute);
                    break;
                case "Amber":
                    themeUri = new Uri("pack://application:,,,/Resources/AmberTheme.xaml", UriKind.Absolute);
                    break;
                case "Emerald":
                    themeUri = new Uri("pack://application:,,,/Resources/EmeraldTheme.xaml", UriKind.Absolute);
                    break;
                default:
                    themeUri = new Uri("pack://application:,,,/Resources/DarkTheme.xaml", UriKind.Absolute);
                    break;
            }
            App.ChangeTheme(themeUri);
        }


        /// <summary>
        /// Calculates an MD5 hash for a file.
        /// </summary>
        private static string CalculateHash(string filePath)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            return BitConverter.ToString(md5.ComputeHash(stream));
        }

        private async Task CheckAndUpdateLauncherAsync()
        {
            // Only run the update check if Unora is the selected game
            var selectedGame = _launcherSettings?.SelectedGame ?? "Unora";
            if (!selectedGame.Equals("Unora", StringComparison.OrdinalIgnoreCase))
                return;

            var serverVersion = await UnoraClient.GetLauncherVersionAsync();
            var localVersion = GetLocalLauncherVersion();

            if (serverVersion != localVersion)
            {
                var bootstrapperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Unora\\UnoraBootstrapper.exe");
                var currentLauncherPath = Process.GetCurrentProcess().MainModule!.FileName!;
                var currentProcessId = Process.GetCurrentProcess().Id;

                var psi = new ProcessStartInfo
                {
                    FileName = bootstrapperPath,
                    Arguments = $"\"{currentLauncherPath}\" {Process.GetCurrentProcess().Id}",
                    UseShellExecute = true
                };
                Process.Start(psi);

                Application.Current.Shutdown();
                Environment.Exit(0);
            }
        }


        private string GetLocalLauncherVersion()
        {
            var exePath = Process.GetCurrentProcess().MainModule!.FileName!;
            return FileVersionInfo.GetVersionInfo(exePath).FileVersion ?? "0";
        }

        /// <summary>
        /// Checks for file updates, downloads any required updates, and updates the UI accordingly.
        /// </summary>
        private async Task CheckForFileUpdates()
        {
            ApplySettings();
            SetUiStateUpdating();

            var apiRoutes = GetCurrentApiRoutes();
            var fileDetails = await UnoraClient.GetFileDetailsAsync(apiRoutes.GameDetails);

            Debug.WriteLine($"[Launcher] Downloading {fileDetails.Count} files for {apiRoutes.GameDetails}");

            var filesToUpdate = fileDetails.Where(NeedsUpdate).ToList();
            Debug.WriteLine($"[Launcher] Files to update: {filesToUpdate.Count}");

            var totalBytesToDownload = filesToUpdate.Sum(f => f.Size);
            var totalDownloaded = 0L;

            if (filesToUpdate.Any() && !ConfirmUpdateProceed())
            {
                ShowMessage("Update cancelled. Please update later.", "Unora Launcher");
                SetUiStateIdle();

                return;
            }

            PrepareProgressBar(totalBytesToDownload);

            await Task.Run(async () =>
            {
                foreach (var fileDetail in filesToUpdate)
                {
                    Debug.WriteLine($"[Launcher] Downloading file: {fileDetail.RelativePath}");
                    PrepareFileProgress(fileDetail.RelativePath, totalDownloaded, totalBytesToDownload);

                    var filePath = GetFilePath(fileDetail.RelativePath);
                    EnsureDirectoryExists(filePath);

                    var fileBytesDownloaded = 0L;

                    var progress = new Progress<UnoraClient.DownloadProgress>(p =>
                    {
                        fileBytesDownloaded = p.BytesReceived;

                        UpdateFileProgress(
                            p.BytesReceived,
                            totalDownloaded,
                            totalBytesToDownload,
                            p.SpeedBytesPerSec);
                    });

                    try
                    {
                        await UnoraClient.DownloadFileAsync(apiRoutes.GameFile(fileDetail.RelativePath), filePath, progress);
                    } catch (Exception ex)
                    {
                        Debug.WriteLine($"[Launcher] Download failed: {ex.Message}");
                        ShowMessage($"Failed to download {fileDetail.RelativePath}: {ex.Message}", "Update Error");
                        // Optionally, break/continue/return depending on your tolerance for errors.
                    }

                    totalDownloaded += fileBytesDownloaded;
                }
            });

            Debug.WriteLine($"[Launcher] All updates completed.");
        }


        /// <summary>
        /// Determines if a file needs to be updated based on its hash.
        /// </summary>
        private bool NeedsUpdate(FileDetail fileDetail)
        {
            var filePath = GetFilePath(fileDetail.RelativePath);
            if (!File.Exists(filePath))
                return true;

            return !CalculateHash(filePath).Equals(fileDetail.Hash, StringComparison.OrdinalIgnoreCase);
        }

        private string GetFilePath(string relativePath) =>
            Path.Combine(_launcherSettings?.SelectedGame ?? CONSTANTS.UNORA_FOLDER_NAME, relativePath);
        
        private void EnsureDirectoryExists(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath)!;
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }

        private bool ConfirmUpdateProceed()
        {
            var lockWindow = new UpdateLockWindow();
            var result = lockWindow.ShowDialog();
            return result == true;
        }

        private void ShowMessage(string message, string title) =>
            MessageBox.Show(message, title);

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
                ProgressBytes.Text = $"{FormatBytes(downloaded)} of {FormatBytes(total)}";
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
                ProgressBytes.Text = $"{FormatBytes(totalDownloaded + bytesReceived)} of {FormatBytes(totalBytesToDownload)}";
                ProgressSpeed.Text = $"@ {FormatSpeed(speedBytesPerSec)}";
                DownloadProgressBar.Value = totalDownloaded + bytesReceived;
            });
        #endregion

        #region Formatting

        private static string FormatBytes(long bytes)
        {
            if (bytes < 0) return "??";
            if (bytes > 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
            if (bytes > 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F2} MB";
            if (bytes > 1024)
                return $"{bytes / 1024.0:F2} KB";

            return $"{bytes} B";
        }

        private static string FormatSpeed(double bytesPerSec)
        {
            if (bytesPerSec > 1024 * 1024)
                return $"{bytesPerSec / (1024.0 * 1024.0):F2} MB/s";
            if (bytesPerSec > 1024)
                return $"{bytesPerSec / 1024.0:F2} KB/s";

            return $"{bytesPerSec:F2} B/s";
        }

        #endregion

        #region System Tray / UI Initialization

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void CogButton_Click(object sender, RoutedEventArgs e)
        {
            if (_launcherSettings == null)
            {
                // Attempt to load or use defaults if ApplySettings hasn't run or failed
                try {
                    _launcherSettings = FileService.LoadSettings(LauncherSettingsPath);
                    if (_launcherSettings == null) // If still null after attempting load
                    {
                        _launcherSettings = new Settings(); // Fallback to default settings
                    }
                } catch { 
                    _launcherSettings = new Settings(); // Fallback to default settings on error
                }
            }
            var settingsWindow = new SettingsWindow(this, _launcherSettings);
            settingsWindow.Owner = this; // Ensure SettingsWindow is owned by MainWindow
            settingsWindow.Show();
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
        public async void ReloadSettingsAndRefresh()
        {
            ApplySettings();           // Load settings from disk (SelectedGame, etc.)
            SetWindowTitle();          // Update the window title everywhere
            await LoadAndBindGameUpdates(); // Reload news/patches for the selected server
            await CheckForFileUpdates();    // Check/download updates for selected server
            Dispatcher.BeginInvoke(new Action(SetUiStateComplete));
        }
        public void ReloadSettingsAndRefreshLocal()
        {
            ApplySettings(); // Reloads from disk into _launcherSettings
            // Optionally: Re-fetch game updates and other game-specific info
            _ = LoadAndBindGameUpdates();
        }

        private (string folder, string exe) GetGameLaunchInfo(string selectedGame) =>
            // You can load this from a config file for extensibility if needed.
            selectedGame switch
            {
                "Unora"   => ("Unora", "Unora.exe"),
                "Legends" => ("Legends", "Client.exe"),
                // Add more as needed
                _ => ("Unora", "Unora.exe") // Fallback
            };

        private void Launch(object sender, EventArgs e)
        {
            (var ipAddress, var serverPort) = GetServerConnection();

            // Use SelectedGame from your settings
            var selectedGame = _launcherSettings?.SelectedGame ?? "Unora";
            (var gameFolder, var gameExe) = GetGameLaunchInfo(selectedGame);

            // Build the full path to the executable
            var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, gameFolder, gameExe);

            using var process = SuspendedProcess.Start(exePath);

            try
            {
                PatchClient(process, ipAddress, serverPort);

                if (UseDawndWindower)
                {
                    var processPtr = NativeMethods.OpenProcess(ProcessAccessFlags.FullAccess, true, process.ProcessId);
                    InjectDll(processPtr);
                }

                // Optionally, set window title to the selected game name
                _ = RenameGameWindowAsync(Process.GetProcessById(process.ProcessId), selectedGame);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UnableToPatchClient: {ex.Message}");
            }
        }


        private (IPAddress, int) GetServerConnection()
        {
            if (UseLocalhost)
                return (ResolveHostname("127.0.0.1"), 4200);

            return (ResolveHostname("chaotic-minds.dynu.net"), 6900);
        }

        private static IPAddress ResolveHostname(string hostname)
        {
            // Lookup the server hostname (via DNS)
            var hostEntry = Dns.GetHostEntry(hostname);

            // Find the IPv4 addresses
            var ipAddresses =
                from ip in hostEntry.AddressList
                where ip.AddressFamily == AddressFamily.InterNetwork
                select ip;

            return ipAddresses.FirstOrDefault();
        }

        private void PatchClient(SuspendedProcess process, IPAddress serverIPAddress, int serverPort)
        {
            using var stream = new ProcessMemoryStream(process.ProcessId);
            using var patcher = new RuntimePatcher(ClientVersion.Version741, stream, true);

            patcher.ApplyServerHostnamePatch(serverIPAddress);
            patcher.ApplyServerPortPatch(serverPort);

            if (SkipIntro)
                patcher.ApplySkipIntroVideoPatch();

            patcher.ApplyMultipleInstancesPatch();
        }

        private void InjectDll(IntPtr accessHandle)
        {
            const string DLL_NAME = "dawnd.dll";
            var nameLength = DLL_NAME.Length + 1;

            // Allocate memory and write the DLL name to target process
            var allocate = NativeMethods.VirtualAllocEx(
                accessHandle, IntPtr.Zero, (IntPtr)nameLength, 0x1000, 0x40);

            NativeMethods.WriteProcessMemory(
                accessHandle, allocate, DLL_NAME, (UIntPtr)nameLength, out _);

            var injectionPtr = NativeMethods.GetProcAddress(
                NativeMethods.GetModuleHandle("kernel32.dll"), "LoadLibraryA");

            if (injectionPtr == UIntPtr.Zero)
            {
                MessageBox.Show(this, "Injection pointer was null.", "Injection Error");
                return;
            }

            var thread = NativeMethods.CreateRemoteThread(
                accessHandle, IntPtr.Zero, IntPtr.Zero, injectionPtr, allocate, 0, out _);

            if (thread == IntPtr.Zero)
            {
                MessageBox.Show(this, "Remote injection thread was null. Try again...", "Injection Error");
                return;
            }

            var result = NativeMethods.WaitForSingleObject(thread, 10 * 1000);

            if (result != WaitEventResult.Signaled)
            {
                MessageBox.Show(this, "Injection thread timed out, or signaled incorrectly. Try again...", "Injection Error");
                if (thread != IntPtr.Zero)
                    NativeMethods.CloseHandle(thread);
                return;
            }

            NativeMethods.VirtualFreeEx(accessHandle, allocate, (UIntPtr)0, 0x8000);
            if (thread != IntPtr.Zero)
                NativeMethods.CloseHandle(thread);
        }

        private async Task RenameGameWindowAsync(Process process, string newTitle)
        {
            for (var i = 0; i < 20; i++)
            {
                process.Refresh();

                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    NativeMethods.SetWindowText(process.MainWindowHandle, newTitle);
                    break;
                }
                await Task.Delay(100);
            }
        }

        private void SetWindowTitle()
        {
            var selectedGame = _launcherSettings?.SelectedGame?.Trim() ?? "Unora";
            var title = selectedGame switch
            {
                "Legends" => "Legends: Age of Chaos",
                "Unora"   => "Unora: Elemental Harmony",
                _         => $"Unora Launcher"
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
                await CheckForFileUpdates();
                await CheckAndUpdateLauncherAsync();
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
            finally
            {
                // Always restore UI, no matter what happened.
                Dispatcher.BeginInvoke(new Action(SetUiStateComplete));
            }
        }


        private GameApiRoutes GetCurrentApiRoutes()
        {
            // Use your actual API base URL; this will pick the right one for Debug/Release from CONSTANTS
            var baseUrl = CONSTANTS.BASE_API_URL.TrimEnd('/');
            var selectedGame = string.IsNullOrWhiteSpace(_launcherSettings?.SelectedGame)
                ? CONSTANTS.UNORA_FOLDER_NAME // Default to "Unora" if not set
                : _launcherSettings.SelectedGame;

            return new GameApiRoutes(baseUrl, selectedGame);
        }

        
        public async Task LoadAndBindGameUpdates()
        {
            var apiRoutes = GetCurrentApiRoutes();
            var gameUpdates = await UnoraClient.GetGameUpdatesAsync(apiRoutes.GameUpdates);
            GameUpdatesControl.DataContext = new { GameUpdates = gameUpdates };
        }

        private void OpenGameUpdate(GameUpdate gameUpdate)
        {
            var detailView = new GameUpdateDetailView(gameUpdate);
            detailView.ShowDialog();
        }

        public static void LogException(Exception e)
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LauncherSettings", "log.txt");

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(logPath, $"{DateTime.Now}: {e}\n");
            }
            catch
            {
                // Suppress logging errors
            }
        }

        #endregion

        public void SaveSettings(Settings settings)
        {
            FileService.SaveSettings(settings, LauncherSettingsPath);
            _launcherSettings = settings; // Update the local field

            // Update MainWindow properties to reflect the newly saved settings
            UseDawndWindower = _launcherSettings.UseDawndWindower;
            UseLocalhost = _launcherSettings.UseLocalhost;
            SkipIntro = _launcherSettings.SkipIntro;
            // Note: SelectedTheme is handled by App.ChangeTheme and ApplySettings directly.
        }
    }
}