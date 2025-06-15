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
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using InputSimulatorStandard;
using InputSimulatorStandard.Native; // Added for CancelEventArgs
using UnoraLaunchpad; // Added for PasswordHelper and Character
using UnoraLaunchpad.Definitions;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using MouseButton = System.Windows.Input.MouseButton;

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

            // Ensure SavedCharacters list exists
            if (_launcherSettings.SavedCharacters == null)
            {
                _launcherSettings.SavedCharacters = new System.Collections.Generic.List<Character>();
            }

            // Password migration logic
            bool settingsModified = false;
            if (_launcherSettings.SavedCharacters != null)
            {
                foreach (var character in _launcherSettings.SavedCharacters)
                {
                    // Check if migration is needed: has old plaintext password but no new hash
                    if (string.IsNullOrEmpty(character.PasswordHash) && !string.IsNullOrEmpty(character.Password))
                    {
                        try
                        {
                            var salt = PasswordHelper.GenerateSalt();
                            character.PasswordHash = PasswordHelper.HashPassword(character.Password, salt);
                            character.Salt = salt;
                            character.Password = null; // Clear the plaintext password
                            settingsModified = true;
                            System.Diagnostics.Debug.WriteLine($"Migrated password for character: {character.Username}");
                        }
                        catch (Exception ex)
                        {
                            // Log or handle migration error for a specific character
                            System.Diagnostics.Debug.WriteLine($"Error migrating password for character {character.Username}: {ex.Message}");
                        }
                    }
                }
            }

            if (settingsModified)
            {
                SaveSettings(_launcherSettings); // Persist migrated passwords
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

            // Apply window dimensions if they are valid
            if (_launcherSettings.WindowWidth > 0 && _launcherSettings.WindowHeight > 0)
            {
                Width = _launcherSettings.WindowWidth;
                Height = _launcherSettings.WindowHeight;
            }

            // Apply window position if it's valid and on-screen
            // Avoid applying if both Left and Top are 0, as this might be an uninitialized state
            // or could make the window appear at an awkward default position for some systems.
            // WindowStartupLocation="CenterScreen" in XAML handles initial centering if position is not set.
            if (_launcherSettings.WindowLeft != 0 || _launcherSettings.WindowTop != 0)
            {
                // Ensure the window is placed mostly on screen.
                // Use current Width/Height which might have been set from settings or default XAML values.
                double maxLeft = SystemParameters.VirtualScreenWidth - Width;
                double maxTop = SystemParameters.VirtualScreenHeight - Height;

                // Basic clamp to ensure top-left is within screen and not excessively off-screen
                Left = Math.Min(Math.Max(0, _launcherSettings.WindowLeft), maxLeft);
                Top = Math.Min(Math.Max(0, _launcherSettings.WindowTop), maxTop);
            }

            RefreshSavedCharactersComboBox(); // Populate/update the ComboBox
        }

        private void RefreshSavedCharactersComboBox()
        {
            SavedCharactersComboBox.Items.Clear();
            SavedCharactersComboBox.Items.Add("All");

            if (_launcherSettings?.SavedCharacters != null && _launcherSettings.SavedCharacters.Any())
            {
                foreach (var character in _launcherSettings.SavedCharacters)
                {
                    SavedCharactersComboBox.Items.Add(character.Username);
                }
                // By default, "All" can remain selected or select the first actual character if desired.
                // SavedCharactersComboBox.SelectedItem = _launcherSettings.SavedCharacters.First().Username;
            }

            // Ensure "All" is selected if it's the only item or by default.
            SavedCharactersComboBox.SelectedItem = "All";

            // Optionally, disable LaunchSavedBtn if no characters exist beyond "All"
            LaunchSavedBtn.IsEnabled = _launcherSettings?.SavedCharacters != null && _launcherSettings.SavedCharacters.Any();
        }


        private void PatchNotesButton_Click(object sender, RoutedEventArgs e)
        {
            var patchWindow = new PatchNotesWindow();
            patchWindow.Owner = this;
            patchWindow.ShowDialog();
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
            if (UseLocalhost) // or this.UseLocalhost, or _launcherSettings.UseLocalhost
            {
                return true; // Skip showing the window if UseLocalhost is true
            }
            var lockWindow = new UpdateLockWindow();
            var result = lockWindow.ShowDialog();

            if (lockWindow.UserSkippedClosingClients) // Added this block
            {
                MessageBox.Show(
                    "You've chosen to skip closing active game clients. Game files may not update correctly, and you might encounter incorrect assets in-game until all clients are closed and the launcher performs a full update check.",
                    "Update Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

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
            ApplySettings();           // Load settings from disk (SelectedGame, etc.) / Repopulates ComboBox
            SetWindowTitle();          // Update the window title everywhere
            await LoadAndBindGameUpdates(); // Reload news/patches for the selected server
            await CheckForFileUpdates();    // Check/download updates for selected server
            Dispatcher.BeginInvoke(new Action(SetUiStateComplete));
        }
        public void ReloadSettingsAndRefreshLocal()
        {
            ApplySettings(); // Reloads from disk into _launcherSettings / Repopulates ComboBox
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

        // Placeholder for ShowPasswordDialog in MainWindow.xaml.cs
        private string ShowPasswordDialog(string username)
        {
            // This method will be fully implemented in the next step
            // using the PasswordPromptDialog.
            // For now, this placeholder allows compilation of LaunchSavedBtn_Click.
            PasswordPromptDialog dialog = new PasswordPromptDialog(username);
            dialog.Owner = this; // Set the owner to the MainWindow instance
            if (dialog.ShowDialog() == true) // ShowDialog() returns a bool? (nullable boolean)
            {
                return dialog.Password;
            }
            return null; // Or string.Empty, depending on how cancellation should be handled
        }

        private async void LaunchSavedBtn_Click(object sender, RoutedEventArgs e)
        {
            // Ensure _launcherSettings is up-to-date (though ApplySettings usually handles this on load)
            // ApplySettings(); // Not strictly needed here if UI always reflects current _launcherSettings

            var selectedItem = SavedCharactersComboBox.SelectedItem as string;

            if (string.IsNullOrEmpty(selectedItem) || _launcherSettings?.SavedCharacters == null || !_launcherSettings.SavedCharacters.Any())
            {
                MessageBox.Show("No saved accounts or no account selected. Please add accounts via Settings or select one.",
                                "Launch Saved Client", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            LaunchBtn.IsEnabled = false;
            LaunchSavedBtn.IsEnabled = false;

            try
            {
                if (selectedItem == "All")
                {
                    bool anyLaunched = false;
                    foreach (var character in _launcherSettings.SavedCharacters.ToList()) // ToList to allow modification if needed, though not here
                    {
                        string password = ShowPasswordDialog(character.Username); // Placeholder
                        if (!string.IsNullOrEmpty(password))
                        {
                            character.Password = password; // Temporarily set for LaunchAndLogin
                            await LaunchAndLogin(character);
                            character.Password = null; // Clear password after use
                            anyLaunched = true;
                        }
                        else
                        {
                            Debug.WriteLine($"Password not provided for {character.Username}. Skipping launch.");
                            // Optionally, inform user via a non-blocking message or log
                        }
                    }
                    if (!anyLaunched)
                    {
                         MessageBox.Show("No accounts were launched. Ensure passwords are provided when prompted.", "Launch All", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else // Specific character selected
                {
                    var characterToLaunch = _launcherSettings.SavedCharacters.FirstOrDefault(c => c.Username == selectedItem);
                    if (characterToLaunch != null)
                    {
                        string password = ShowPasswordDialog(characterToLaunch.Username); // Placeholder
                        if (!string.IsNullOrEmpty(password))
                        {
                            characterToLaunch.Password = password; // Temporarily set for LaunchAndLogin
                            await LaunchAndLogin(characterToLaunch);
                            characterToLaunch.Password = null; // Clear password after use
                        }
                        else
                        {
                            MessageBox.Show($"Launch canceled for {characterToLaunch.Username} as no password was provided.", "Launch Canceled", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    else
                    {
                        MessageBox.Show($"Selected character '{selectedItem}' not found. Please check settings.", "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (NotImplementedException nie) // This catch block might no longer be necessary if the placeholder is fully replaced.
                                                // However, other NotImplementedExceptions could occur if other parts are placeholders.
                                                // For now, it's fine to leave it, or it can be removed if ShowPasswordDialog is the only source.
                                                // Let's assume for now it might catch other issues, or we can remove it if confident.
                                                // Given the task, this specific NIE for ShowPasswordDialog should be gone.
            {
                MessageBox.Show(this, $"Login for '{selectedItem}' requires password entry: {nie.Message}", "Password Dialog Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                LogException(ex);
            }
            finally
            {
                LaunchBtn.IsEnabled = true;
                LaunchSavedBtn.IsEnabled = _launcherSettings?.SavedCharacters != null && _launcherSettings.SavedCharacters.Any(); // Re-enable based on if characters exist
            }
        }

        private async Task LaunchAndLogin(Character character)
        {
            Process gameProcess = null;
            int gameProcessId = 0;
            try
            {
                (var ipAddress, var serverPort) = GetServerConnection();
                // Ensure _launcherSettings is used, ApplySettings() at start of LaunchSaveBtn_Click should handle this.
                var selectedGame = _launcherSettings.SelectedGame ?? "Unora"; 
                (var gameFolder, var gameExe) = GetGameLaunchInfo(selectedGame);
                var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, gameFolder, gameExe);

                using (var suspendedProcess = SuspendedProcess.Start(exePath))
                {
                    gameProcessId = suspendedProcess.ProcessId; // Capture PID
                    PatchClient(suspendedProcess, ipAddress, serverPort);

                    // Use 'this.UseDawndWindower' which is synced by ApplySettings()
                    if (this.UseDawndWindower) 
                    {
                        IntPtr processHandleForInjection = NativeMethods.OpenProcess(ProcessAccessFlags.FullAccess, true, gameProcessId);
                        if (processHandleForInjection != IntPtr.Zero)
                        {
                            InjectDll(processHandleForInjection);
                            NativeMethods.CloseHandle(processHandleForInjection);
                        }
                        else
                        {
                            Debug.WriteLine($"[LaunchAndLogin] Failed to open process for injection. Error: {NativeMethods.GetLastError()}");
                        }
                    }
                } // suspendedProcess is disposed and resumed here

                if (gameProcessId == 0) {
                    MessageBox.Show("Failed to get game process ID during launch.", "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                try
                {
                    gameProcess = Process.GetProcessById(gameProcessId);
                }
                catch (ArgumentException) // Catches if process isn't running
                {
                    MessageBox.Show("Game process is not running after launch attempt. It might have crashed or failed to start.", "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                if (gameProcess == null || gameProcess.HasExited) { 
                    MessageBox.Show("Failed to start or patch the game process, or it exited prematurely.", "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // This method already waits for MainWindowHandle to be available
                await RenameGameWindowAsync(gameProcess, selectedGame); 

                if (gameProcess.MainWindowHandle == IntPtr.Zero)
                {
                    // If RenameGameWindowAsync didn't find it (it should have after its loop), try one more time.
                    await Task.Delay(2000); 
                    gameProcess.Refresh(); 
                    if (gameProcess.MainWindowHandle == IntPtr.Zero) {
                         MessageBox.Show("Game window handle could not be found. Cannot proceed with automated login.", "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
                         return;
                    }
                }
                
                await PerformAutomatedLogin(character.Username, character.Password, gameProcess);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while launching or logging in: {ex.Message}", "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
                LogException(ex); // MainWindow.LogException is static
            }
        }

        private async Task PerformAutomatedLogin(string username, string password, Process gameProc)
        {
            try
            {
                if (gameProc.MainWindowHandle == IntPtr.Zero)
                {
                    gameProc.Refresh();
                    await Task.Delay(1000);
                    if (gameProc.MainWindowHandle == IntPtr.Zero)
                    {
                        MessageBox.Show("Game window not found for automated login (PerformAutomatedLogin).",
                            "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // Bring game window to the front
                NativeMethods.SetForegroundWindow(gameProc.MainWindowHandle.ToInt32());
                await Task.Delay(1000); // Allow window to focus

                var inputSimulator = new InputSimulator();

                // Simulate ENTER to start login sequence
                await Task.Delay(200); // Let the login screen settle
                BlockInput(true);
                inputSimulator.Keyboard.KeyPress(VirtualKeyCode.RETURN);
                await Task.Delay(500);

                // Move mouse to simulated "Continue" button (approximate)
                var screenPoint = GetRelativeScreenPoint(gameProc.MainWindowHandle, 0.20, 0.66);
                MoveAndClickPoint(screenPoint);
                await Task.Delay(500);

                // Enter username
                inputSimulator.Keyboard.TextEntry(username);
                await Task.Delay(250);
                inputSimulator.Keyboard.KeyPress(VirtualKeyCode.TAB);
                await Task.Delay(400);

                
                // Enter password
                foreach (var c in password)
                {
                    inputSimulator.Keyboard.KeyPress((VirtualKeyCode)VkKeyScan(c));
                    await Task.Delay(50); // small delay to mimic natural typing
                }

                await Task.Delay(200);
                inputSimulator.Keyboard.KeyPress(VirtualKeyCode.RETURN);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Login automation failed: {ex.Message}", "Login Automation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BlockInput(false); // Make absolutely sure to unblock input
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BlockInput(bool fBlockIt);

        
        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        private const uint MouseeventfLeftdown = 0x0002;
        private const uint MouseeventfLeftup = 0x0004;

        private void MoveAndClickPoint(System.Drawing.Point point)
        {
            SetCursorPos(point.X, point.Y);
            Thread.Sleep(100);
            mouse_event(MouseeventfLeftdown, (uint)point.X, (uint)point.Y, 0, 0);
            Thread.Sleep(50);
            mouse_event(MouseeventfLeftup, (uint)point.X, (uint)point.Y, 0, 0);
        }

        
        private System.Drawing.Point GetRelativeScreenPoint(IntPtr hwnd, double relativeX, double relativeY)
        {
            var rect = new NativeMethods.Rect();
            if (!NativeMethods.GetWindowRect(hwnd, ref rect))
                return new System.Drawing.Point(0, 0); // fallback if invalid

            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;

            var screenX = rect.Left + (int)(width * relativeX);
            var screenY = rect.Top + (int)(height * relativeY);

            return new System.Drawing.Point(screenX, screenY);
        }
        
        
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
        
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_launcherSettings != null)
            {
                // Only save size and position if the window is in its normal state
                if (WindowState == WindowState.Normal)
                {
                    _launcherSettings.WindowHeight = ActualHeight;
                    _launcherSettings.WindowWidth = ActualWidth;
                    _launcherSettings.WindowTop = Top;
                    _launcherSettings.WindowLeft = Left;
                }
                FileService.SaveSettings(_launcherSettings, LauncherSettingsPath);
            }
        }
    }
}