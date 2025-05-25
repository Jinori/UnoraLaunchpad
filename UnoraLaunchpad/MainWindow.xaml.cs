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
        public void ApplySettings()
        {
            var settings = FileService.LoadSettings(LauncherSettingsPath);
            UseDawndWindower = settings.UseDawndWindower;
            UseLocalhost = settings.UseLocalhost;
            SkipIntro = settings.SkipIntro;
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
                    UseShellExecute = true  // <-- This is crucial
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

            var fileDetails = await UnoraClient.GetFileDetailsAsync();
            var filesToUpdate = fileDetails
                .Where(NeedsUpdate)
                .ToList();

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
                    PrepareFileProgress(fileDetail.RelativePath, totalDownloaded, totalBytesToDownload);

                    var filePath = GetFilePath(fileDetail.RelativePath);
                    EnsureDirectoryExists(filePath);

                    var fileBytesDownloaded = 0L;

                    var progress = new Progress<UnoraClient.DownloadProgress>(p =>
                    {
                        fileBytesDownloaded = p.BytesReceived; // capture for use after download
                        UpdateFileProgress(p.BytesReceived, totalDownloaded, totalBytesToDownload, p.SpeedBytesPerSec);
                    });

                    await UnoraClient.DownloadFileAsync(fileDetail.RelativePath, filePath, progress);
                    totalDownloaded += fileBytesDownloaded;

                }
            });
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
            Path.Combine(CONSTANTS.UNORA_FOLDER_NAME, relativePath);

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
            var settingsWindow = new SettingsWindow();
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

        private void Launch(object sender, EventArgs e)
        {
            var (ipAddress, serverPort) = GetServerConnection();

            using var process = SuspendedProcess.Start(AppDomain.CurrentDomain.BaseDirectory + "\\Unora\\Unora.exe");

            try
            {
                PatchClient(process, ipAddress, serverPort);

                if (UseDawndWindower)
                {
                    var processPtr = NativeMethods.OpenProcess(ProcessAccessFlags.FullAccess, true, process.ProcessId);
                    InjectDll(processPtr);
                }

                _ = RenameGameWindowAsync(Process.GetProcessById(process.ProcessId));
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

        private async Task RenameGameWindowAsync(Process process)
        {
            const string NEW_TITLE = "Unora";
            for (var i = 0; i < 20; i++)
            {
                // Refresh the process to update MainWindowHandle
                process.Refresh();

                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    NativeMethods.SetWindowText(process.MainWindowHandle, NEW_TITLE);
                    break;
                }
                await Task.Delay(100);
            }
        }


        #endregion

        #region Game Updates

        private async void Launcher_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadAndBindGameUpdates();
                await CheckForFileUpdates();
                await CheckAndUpdateLauncherAsync();
                Dispatcher.BeginInvoke(SetUiStateComplete);
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
        }

        public async Task LoadAndBindGameUpdates()
        {
            var gameUpdates = await UnoraClient.GetGameUpdatesAsync();
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

        public void SaveSettings(Settings settings) => FileService.SaveSettings(settings, LauncherSettingsPath);
    }
}
