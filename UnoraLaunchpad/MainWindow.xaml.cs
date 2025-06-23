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
using InputSimulatorStandard.Native;
using UnoraLaunchpad.Definitions;
using Application = System.Windows.Application;
using System.Windows.Interop; // Required for HwndSource
using System.Collections.Generic; // Required for Dictionary
// UnoraLaunchpad; // Added for PasswordHelper and Character - already in namespace
using MessageBox = System.Windows.MessageBox;
using MouseButton = System.Windows.Input.MouseButton;

namespace UnoraLaunchpad
{
    public sealed partial class MainWindow
    {
        private static readonly string LauncherSettingsPath = "LauncherSettings/settings.json";

        private readonly FileService FileService = new();
        private readonly UnoraClient UnoraClient = new();
        private Settings _launcherSettings;
        private NotifyIcon NotifyIcon;

        // === Fields for Global Hotkeys ===
        private HwndSource _hwndSource;
        private const int WM_HOTKEY = 0x0312;
        private Dictionary<int, string> _registeredHotkeys; // ID -> ActionSequence
        private int _currentHotkeyId = 9000; // Starting ID for hotkeys
        // === End Fields for Global Hotkeys ===

        public ObservableCollection<GameUpdate> GameUpdates { get; } = new();

        // These properties are bound to settings, ensure they are updated when _launcherSettings changes.
        // It might be better to bind directly to _launcherSettings properties in XAML if possible,
        // or ensure these are consistently updated.
        public bool SkipIntro
        {
            get => _launcherSettings?.SkipIntro ?? false;
            set { if (_launcherSettings != null) _launcherSettings.SkipIntro = value; }
        }
        public bool UseDawndWindower
        {
            get => _launcherSettings?.UseDawndWindower ?? false;
            set { if (_launcherSettings != null) _launcherSettings.UseDawndWindower = value; }
        }
        public bool UseLocalhost
        {
            get => _launcherSettings?.UseLocalhost ?? false;
            set { if (_launcherSettings != null) _launcherSettings.UseLocalhost = value; }
        }
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
            _registeredHotkeys = new Dictionary<int, string>();
        }


        // === Global Hotkey System ===
         protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            _hwndSource?.AddHook(WndProc);
            // Initial registration after settings are loaded (Launcher_Loaded calls ApplySettings then RegisterGlobalHotkeys)
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && _launcherSettings.IsComboSystemEnabled)
            {
                int hotkeyId = wParam.ToInt32();
                if (_registeredHotkeys.TryGetValue(hotkeyId, out string actionSequence))
                {
                    System.Diagnostics.Debug.WriteLine($"Hotkey {hotkeyId} pressed, sequence: {actionSequence}");
                    ExecuteMacroAction(actionSequence);
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private async void ExecuteMacroAction(string actionSequence)
        {
            IntPtr activeWindowHandle = NativeMethods.GetForegroundWindow();
            if (activeWindowHandle == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("No active window found. Macro not executed.");
                return;
            }

            int length = NativeMethods.GetWindowTextLength(activeWindowHandle);
            if (length == 0)
            {
                System.Diagnostics.Debug.WriteLine("Active window has no title. Macro not executed.");
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder(length + 1);
            NativeMethods.GetWindowText(activeWindowHandle, sb, sb.Capacity);
            string activeWindowTitle = sb.ToString();

            bool isGameWindow = false;
            if (activeWindowTitle.Equals("Darkages", StringComparison.OrdinalIgnoreCase) ||
                activeWindowTitle.Equals("Unora", StringComparison.OrdinalIgnoreCase))
            {
                isGameWindow = true;
            }
            else if (_launcherSettings?.SavedCharacters != null)
            {
                foreach (var character in _launcherSettings.SavedCharacters)
                {
                    if (activeWindowTitle.Equals(character.Username, StringComparison.OrdinalIgnoreCase))
                    {
                        isGameWindow = true;
                        break;
                    }
                }
            }

            if (!isGameWindow)
            {
                System.Diagnostics.Debug.WriteLine($"Active window ('{activeWindowTitle}') is not a recognized game window. Macro not executed.");
                return;
            }

            // The window is already active, so no need to call SetForegroundWindow.
            // A small delay might still be beneficial for reliability with SendInput.
            await Task.Delay(50);

            var inputSimulator = new InputSimulator();
            var actions = ComboParser.ParseActionSequence(actionSequence);

            foreach (var action in actions)
            {
                switch (action.Type)
                {
                    case ComboActionType.SendText:
                        await SendStringAsKeyPressesAsync(inputSimulator, (string)action.Argument);
                        break;
                    case ComboActionType.SendChar: // New case for individual characters
                        if (action.Argument is char charToSend)
                        {
                            await SendCharAsync(inputSimulator.Keyboard, charToSend);
                        }
                        break;
                    case ComboActionType.Wait:
                        await Task.Delay((int)action.Argument);
                        break;
                    case ComboActionType.KeyPress:
                        inputSimulator.Keyboard.KeyPress((VirtualKeyCode)action.Argument);
                        break;
                    case ComboActionType.KeyDown:
                        inputSimulator.Keyboard.KeyDown((VirtualKeyCode)action.Argument);
                        break;
                    case ComboActionType.KeyUp:
                        inputSimulator.Keyboard.KeyUp((VirtualKeyCode)action.Argument);
                        break;
                    case ComboActionType.SendKeySequence:
                        // This case might become obsolete or less used with the new parser,
                        // but keeping it for now for any legacy combos or specific uses.
                        await ProcessSendKeySequenceAsync(inputSimulator, (string)action.Argument);
                        System.Diagnostics.Debug.WriteLine($"Executing SendKeySequence: {(string)action.Argument}");
                        break;
                }
                await Task.Delay(50); // Small delay between actions
            }
        }

        private async Task SendStringAsKeyPressesAsync(InputSimulator simulator, string textToSend)
        {
            foreach (char c in textToSend)
            {
                await SendCharAsync(simulator.Keyboard, c);
            }
        }

        private async Task ProcessSendKeySequenceAsync(InputSimulator simulator, string sequence)
        {
            // Simple parser for {KEY} syntax combined with literal strings
            // Example: "Hello{ENTER}World{TAB}"
            int i = 0;
            while (i < sequence.Length)
            {
                if (sequence[i] == '{')
                {
                    int endIndex = sequence.IndexOf('}', i);
                    if (endIndex == -1) // No closing brace, treat rest as literal
                    {
                        await SendStringAsKeyPressesAsync(simulator, sequence.Substring(i));
                        break;
                    }

                    string keyName = sequence.Substring(i + 1, endIndex - i - 1);
                    if (Enum.TryParse<VirtualKeyCode>(keyName, true, out var vkCode))
                    {
                        simulator.Keyboard.KeyPress(vkCode);
                    }
                    else
                    {
                        // If not a VK code, could be a special command or just literal text like "{literal}"
                        // For now, sending as literal if not a VK.
                        await SendStringAsKeyPressesAsync(simulator, $"{{{keyName}}}");
                    }
                    i = endIndex + 1;
                }
                else
                {
                    int nextBrace = sequence.IndexOf('{', i);
                    if (nextBrace == -1) // No more special keys, send rest of string
                    {
                        await SendStringAsKeyPressesAsync(simulator, sequence.Substring(i));
                        break;
                    }
                    else // Send text up to the next special key
                    {
                        await SendStringAsKeyPressesAsync(simulator, sequence.Substring(i, nextBrace - i));
                        i = nextBrace;
                    }
                }
                await Task.Delay(20); // Small delay between parts of a sequence
            }
        }


        private void RegisterGlobalHotkeys()
        {
            if (_hwndSource == null || _hwndSource.Handle == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("HwndSource not ready for hotkey registration.");
                return;
            }
            UnregisterGlobalHotkeys(); // Clear existing before registering new ones

            if (!_launcherSettings.IsComboSystemEnabled || _launcherSettings.Combos == null)
            {
                return;
            }

            foreach (var comboEntry in _launcherSettings.Combos)
            {
                if (ComboParser.TryParseTriggerKey(comboEntry.Key, out uint modifiers, out uint vkCode))
                {
                    int hotkeyId = _currentHotkeyId++;
                    if (NativeMethods.RegisterHotKey(_hwndSource.Handle, hotkeyId, modifiers, vkCode))
                    {
                        _registeredHotkeys.Add(hotkeyId, comboEntry.Value);
                        System.Diagnostics.Debug.WriteLine($"Registered hotkey ID {hotkeyId} for {comboEntry.Key}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to register hotkey for {comboEntry.Key}. Error: {Marshal.GetLastWin32Error()}");
                        // Consider notifying user or logging more formally
                    }
                }
                else
                {
                     System.Diagnostics.Debug.WriteLine($"Could not parse trigger key: {comboEntry.Key}");
                }
            }
        }

        private void UnregisterGlobalHotkeys()
        {
            if (_hwndSource == null || _hwndSource.Handle == IntPtr.Zero) return;

            foreach (var hotkeyId in _registeredHotkeys.Keys)
            {
                NativeMethods.UnregisterHotKey(_hwndSource.Handle, hotkeyId);
                System.Diagnostics.Debug.WriteLine($"Unregistered hotkey ID {hotkeyId}");
            }
            _registeredHotkeys.Clear();
        }
        // === End Global Hotkey System ===


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
             if (_launcherSettings.Combos == null) // Ensure Combos dictionary exists
            {
                _launcherSettings.Combos = new Dictionary<string, string>();
            }


            // Ensure SavedCharacters list exists
            if (_launcherSettings.SavedCharacters == null)
            {
                _launcherSettings.SavedCharacters = new System.Collections.Generic.List<Character>();
            }

            // New Password migration logic to EncryptedPassword
            var settingsModified = false;
            if (_launcherSettings.SavedCharacters != null)
            {
                foreach (var character in _launcherSettings.SavedCharacters)
                {
                    if (!string.IsNullOrEmpty(character.Password)) // Plaintext password exists (oldest format)
                    {
                        // Prioritize migrating plaintext if it exists
                        System.Diagnostics.Debug.WriteLine(
                            $"Migrating plaintext password for character: {character.Username} to encrypted format.");
                        try
                        {
                            character.EncryptedPassword = PasswordHelper.EncryptString(character.Password);
                            character.Password = null; // Clear old plaintext
                            settingsModified = true;
                        }
                        catch (Exception ex)
                        {

                        }
                    }
                    // If EncryptedPassword is already populated, and Password/PasswordHash are null, nothing to do.
                }
            }

            if (settingsModified)
            {
                SaveSettings(_launcherSettings); // Persist migrated passwords and cleared old fields
            }

            // UseDawndWindower = _launcherSettings.UseDawndWindower; // Now handled by property getter/setter
            // SkipIntro = _launcherSettings.SkipIntro; // Now handled by property getter/setter
            // UseLocalhost = _launcherSettings.UseLocalhost; // Now handled by property getter/setter

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
                case "Ruby":
                    themeUri = new Uri("pack://application:,,,/Resources/RubyTheme.xaml", UriKind.Absolute);
                    break;
                case "Sapphire":
                    themeUri = new Uri("pack://application:,,,/Resources/SapphireTheme.xaml", UriKind.Absolute);
                    break;
                case "Topaz":
                    themeUri = new Uri("pack://application:,,,/Resources/TopazTheme.xaml", UriKind.Absolute);
                    break;
                case "Amethyst":
                    themeUri = new Uri("pack://application:,,,/Resources/AmethystTheme.xaml", UriKind.Absolute);
                    break;
                case "Garnet":
                    themeUri = new Uri("pack://application:,,,/Resources/GarnetTheme.xaml", UriKind.Absolute);
                    break;
                case "Pearl":
                    themeUri = new Uri("pack://application:,,,/Resources/PearlTheme.xaml", UriKind.Absolute);
                    break;
                case "Obsidian":
                    themeUri = new Uri("pack://application:,,,/Resources/ObsidianTheme.xaml", UriKind.Absolute);
                    break;
                case "Citrine":
                    themeUri = new Uri("pack://application:,,,/Resources/CitrineTheme.xaml", UriKind.Absolute);
                    break;
                case "Peridot":
                    themeUri = new Uri("pack://application:,,,/Resources/PeridotTheme.xaml", UriKind.Absolute);
                    break;
                case "Aquamarine":
                    themeUri = new Uri("pack://application:,,,/Resources/AquamarineTheme.xaml", UriKind.Absolute);
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
                var maxLeft = SystemParameters.VirtualScreenWidth - Width;
                var maxTop = SystemParameters.VirtualScreenHeight - Height;

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
            LaunchSavedBtn.IsEnabled =
                _launcherSettings?.SavedCharacters != null && _launcherSettings.SavedCharacters.Any();
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
                var bootstrapperPath =
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Unora\\UnoraBootstrapper.exe");
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
                        await UnoraClient.DownloadFileAsync(apiRoutes.GameFile(fileDetail.RelativePath), filePath,
                            progress);
                    }
                    catch (Exception ex)
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
                LaunchSavedBtn.Visibility = Visibility.Collapsed;
                LaunchBtn.Visibility = Visibility.Collapsed;
                DiamondText.Visibility = Visibility.Collapsed;
                SavedCharactersComboBox.Visibility = Visibility.Collapsed;
                ProgressFileName.Text = string.Empty;
                ProgressBytes.Text = "Checking for updates...";
                DownloadProgressBar.IsIndeterminate = true;
            });

        private void SetUiStateIdle() => Dispatcher.Invoke(() => LaunchBtn.IsEnabled = true);

        private void SetUiStateComplete()
        {
            DownloadProgressPanel.Visibility = Visibility.Collapsed;
            StatusLabel.Text = "Update complete.";
            StatusLabel.Visibility = Visibility.Visible;
            LaunchSavedBtn.Visibility = Visibility.Visible;
            LaunchBtn.Visibility = Visibility.Visible;
            DiamondText.Visibility = Visibility.Visible;
            SavedCharactersComboBox.Visibility = Visibility.Visible;
        }

        private void PrepareProgressBar(long totalBytesToDownload) =>
            Dispatcher.Invoke(() =>
            {
                ProgressFileName.Text = string.Empty;
                ProgressBytes.Text = "Applying updates...";
                DownloadProgressBar.IsIndeterminate = false;
                DownloadProgressBar.Minimum = 0;
                DownloadProgressBar.Maximum = totalBytesToDownload > 0 ? totalBytesToDownload : 1;
            });

        private void PrepareFileProgress(string fileName, long downloaded, long total) =>
            Dispatcher.Invoke(() =>
            {
                ProgressFileName.Text = fileName;
                ProgressBytes.Text = $"{FormatBytes(downloaded)} of {FormatBytes(total)}";
                DownloadProgressBar.Value = downloaded;
            });

        private void UpdateFileProgress(
            long bytesReceived,
            long totalDownloaded,
            long totalBytesToDownload,
            double speedBytesPerSec) =>
            Dispatcher.Invoke(() =>
            {
                ProgressBytes.Text =
                    $"{FormatBytes(totalDownloaded + bytesReceived)} of {FormatBytes(totalBytesToDownload)}";
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
                try
                {
                    _launcherSettings = FileService.LoadSettings(LauncherSettingsPath);
                    if (_launcherSettings == null) // If still null after attempting load
                    {
                        _launcherSettings = new Settings(); // Fallback to default settings
                    }
                }
                catch
                {
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
            var iconUri = new Uri("pack://application:,,,/UnoraLaunchpad;component/favicon.ico",
                UriKind.RelativeOrAbsolute);
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
            {
                // Hide(); // Standard behavior is to hide to tray if NotifyIcon is used.
                // If you want it to truly minimize and still be on taskbar, remove Hide()
                // and ensure tray icon logic handles visibility correctly.
                // For now, let's assume standard tray icon behavior: Hide() on minimize.
                 Hide();
            }
            base.OnStateChanged(e);
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void MacroButton_Click(object sender, RoutedEventArgs e)
        {
            // This button is being removed, functionality moved to SettingsWindow.
            // This button is being removed, functionality moved to SettingsWindow.
            // If this method is still called, it means the XAML was not updated.
            // For now, we'll keep the method shell but it should ideally be removed if the button is gone from XAML.
            // Update: The button will be removed from XAML, so this click handler might become dead code.
            // Let's comment it out or remove it if we are sure the XAML part is also removed.
            // For now, let's leave a message box as a fallback.
             MessageBox.Show("The dedicated macro window has been removed. Please use the Settings window to manage combos.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        // The MacroButton_Click handler above will be removed along with the button in XAML.

        #endregion

        #region Launcher Core

        public async void ReloadSettingsAndRefresh()
        {
            ApplySettings(); // Load settings from disk (SelectedGame, etc.) / Repopulates ComboBox
            SetWindowTitle(); // Update the window title everywhere
            await LoadAndBindGameUpdates(); // Reload news/patches for the selected server
            await CheckForFileUpdates(); // Check/download updates for selected server
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
                "Unora" => ("Unora", "Unora.exe"),
                "Legends" => ("Legends", "Client.exe"),
                // Add more as needed
                _ => ("Unora", "Unora.exe") // Fallback
            };

        private void Launch(object sender, EventArgs e)
        {
            var (ipAddress, serverPort) = GetServerConnection();

            // Use SelectedGame from your settings
            var selectedGame = _launcherSettings?.SelectedGame ?? "Unora";
            var (gameFolder, gameExe) = GetGameLaunchInfo(selectedGame);

            // Build the full path to the executable
            var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, gameFolder, gameExe);

            using var process = SuspendedProcess.Start(exePath);

            try
            {
                PatchClient(process, ipAddress, serverPort, false);

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

        private void PatchClient(SuspendedProcess process, IPAddress serverIPAddress, int serverPort, bool autologin)
        {
            using var stream = new ProcessMemoryStream(process.ProcessId);
            using var patcher = new RuntimePatcher(ClientVersion.Version741, stream, true);

            patcher.ApplyServerHostnamePatch(serverIPAddress);
            patcher.ApplyServerPortPatch(serverPort);

            if (SkipIntro || autologin)
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
                MessageBox.Show(this, "Injection thread timed out, or signaled incorrectly. Try again...",
                    "Injection Error");
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
                "Unora" => "Unora: Elemental Harmony",
                _ => $"Unora Launcher"
            };

            Title = title; // OS-level window title
            WindowTitleLabel.Content = title; // Custom title bar label
        }



        #endregion

        private async void LaunchSavedBtn_Click(object sender, RoutedEventArgs e)
        {
            // Ensure _launcherSettings is up-to-date (though ApplySettings usually handles this on load)
            // ApplySettings(); // Not strictly needed here if UI always reflects current _launcherSettings

            var selectedItem = SavedCharactersComboBox.SelectedItem as string;

            if (string.IsNullOrEmpty(selectedItem) || _launcherSettings?.SavedCharacters == null ||
                !_launcherSettings.SavedCharacters.Any())
            {
                MessageBox.Show(
                    "No saved accounts or no account selected. Please add accounts via Settings or select one.",
                    "Launch Saved Client", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            LaunchBtn.IsEnabled = false;
            LaunchSavedBtn.IsEnabled = false;

            try
            {
                if (selectedItem == "All")
                {
                    var anyLaunched = false;
                    foreach (var character in _launcherSettings.SavedCharacters.ToList())
                    {
                        if (!string.IsNullOrEmpty(character.EncryptedPassword))
                        {
                            var decryptedPassword = PasswordHelper.DecryptString(character.EncryptedPassword);
                            if (!string.IsNullOrEmpty(decryptedPassword))
                            {
                                character.Password = decryptedPassword; // Temporarily set for LaunchAndLogin
                                await LaunchAndLogin(character);
                                character.Password = null; // Clear password after use
                                anyLaunched = true;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine(
                                    $"Failed to decrypt password for {character.Username} during 'Launch All'. Skipping.");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"No encrypted password for {character.Username} during 'Launch All'. Skipping.");
                        }
                    }

                    if (!anyLaunched)
                    {
                        MessageBox.Show(
                            "No accounts could be launched. Check passwords in Settings or ensure they are saved correctly.",
                            "Launch All", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else // Specific character selected
                {
                    var characterToLaunch =
                        _launcherSettings.SavedCharacters.FirstOrDefault(c => c.Username == selectedItem);
                    if (characterToLaunch != null)
                    {
                        if (!string.IsNullOrEmpty(characterToLaunch.EncryptedPassword))
                        {
                            var decryptedPassword = PasswordHelper.DecryptString(characterToLaunch.EncryptedPassword);
                            if (!string.IsNullOrEmpty(decryptedPassword))
                            {
                                characterToLaunch.Password = decryptedPassword; // Temporarily set for LaunchAndLogin
                                await LaunchAndLogin(characterToLaunch);
                                characterToLaunch.Password = null; // Clear password after use
                            }
                            else
                            {
                                MessageBox.Show(
                                    $"Failed to decrypt password for {characterToLaunch.Username}. The password may be corrupted, or settings might have been moved from another user/computer. Please re-save the password in Settings.",
                                    "Decryption Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                        else
                        {
                            MessageBox.Show(
                                $"No saved (encrypted) password found for {characterToLaunch.Username}. Please save the password in Settings.",
                                "Password Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    else
                    {
                        MessageBox.Show($"Selected character '{selectedItem}' not found. Please check settings.",
                            "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            // Removed NotImplementedException catch block as ShowPasswordDialog is no longer called.
            catch (Exception ex)
            {
                MessageBox.Show(this, $"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                LogException(ex);
            }
            finally
            {
                LaunchBtn.IsEnabled = true;
                LaunchSavedBtn.IsEnabled = _launcherSettings?.SavedCharacters != null &&
                                           _launcherSettings.SavedCharacters
                                               .Any(); // Re-enable based on if characters exist
            }
        }

        private async Task LaunchAndLogin(Character character)
        {
            try
            {
                var (ipAddress, serverPort) = GetServerConnection();
                // Ensure _launcherSettings is used, ApplySettings() at start of LaunchSaveBtn_Click should handle this.
                var selectedGame = _launcherSettings.SelectedGame ?? "Unora";
                var (gameFolder, gameExe) = GetGameLaunchInfo(selectedGame);
                var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, gameFolder, gameExe);

                var gameProcessId = 0;
                using (var suspendedProcess = SuspendedProcess.Start(exePath))
                {
                    gameProcessId = suspendedProcess.ProcessId; // Capture PID
                    PatchClient(suspendedProcess, ipAddress, serverPort, true);

                    // Use 'this.UseDawndWindower' which is synced by ApplySettings()
                    if (UseDawndWindower)
                    {
                        var processHandleForInjection =
                            NativeMethods.OpenProcess(ProcessAccessFlags.FullAccess, true, gameProcessId);
                        if (processHandleForInjection != IntPtr.Zero)
                        {
                            InjectDll(processHandleForInjection);
                            NativeMethods.CloseHandle(processHandleForInjection);
                        }
                    }
                } // suspendedProcess is disposed and resumed here

                if (gameProcessId == 0)
                {
                    MessageBox.Show("Failed to get game process ID during launch.", "Launch Error", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                Process gameProcess = null;
                try
                {
                    gameProcess = Process.GetProcessById(gameProcessId);
                }
                catch (ArgumentException) // Catches if process isn't running
                {
                    MessageBox.Show(
                        "Game process is not running after launch attempt. It might have crashed or failed to start.",
                        "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (gameProcess == null || gameProcess.HasExited)
                {
                    MessageBox.Show("Failed to start or patch the game process, or it exited prematurely.",
                        "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // This method already waits for MainWindowHandle to be available
                await RenameGameWindowAsync(gameProcess, selectedGame);

                if (gameProcess.MainWindowHandle == IntPtr.Zero)
                {
                    // If RenameGameWindowAsync didn't find it (it should have after its loop), try one more time.
                    await Task.Delay(2000);
                    gameProcess.Refresh();
                    if (gameProcess.MainWindowHandle == IntPtr.Zero)
                    {
                        MessageBox.Show("Game window handle could not be found. Cannot proceed with automated login.",
                            "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                await PerformAutomatedLogin(character.Username, character.Password, gameProcess);
                await RenameGameWindowAsync(gameProcess, character.Username);
                await WaitForClientReady(gameProcess);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while launching or logging in: {ex.Message}", "Launch Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
                    await Task.Delay(2000);
                    if (gameProc.MainWindowHandle == IntPtr.Zero)
                    {
                        MessageBox.Show("Game window not found.", "Login Error", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }
                }

                BlockInput(true);
                NativeMethods.SetForegroundWindow(gameProc.MainWindowHandle.ToInt32());
                await Task.Delay(2500);

                var inputSimulator = new InputSimulator();

                inputSimulator.Keyboard.KeyPress(VirtualKeyCode.RETURN);
                await Task.Delay(1500);

                var screenPoint = GetRelativeScreenPoint(gameProc.MainWindowHandle, 0.20, 0.66);
                MoveAndClickPoint(screenPoint);
                await Task.Delay(750);

                inputSimulator.Keyboard.TextEntry(username);
                await Task.Delay(750);
                inputSimulator.Keyboard.KeyPress(VirtualKeyCode.TAB);
                await Task.Delay(750);

                await TypePasswordAsync(inputSimulator.Keyboard, password);

                await Task.Delay(200);
                inputSimulator.Keyboard.KeyPress(VirtualKeyCode.RETURN);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Login automation failed: {ex.Message}", "Login Automation Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                BlockInput(false); // Make absolutely sure to unblock input
            }
        }

        // Removed DllImport for VkKeyScan as it's now in NativeMethods

        /// <summary>
        /// Sends a single character through an <see cref="IKeyboardSimulator"/>,
        /// automatically holding <c>SHIFT</c> when the scan-code says it’s required
        /// (e.g. uppercase letters or symbols like “!”).
        /// </summary>
        private static async Task SendCharAsync( // This method is used by SendStringAsKeyPressesAsync
            IKeyboardSimulator keyboard,
            char character,
            int interKeyDelayMs = 50) // Default interKeyDelayMs matches original TypePasswordAsync
        {
            // Use the VkKeyScan from NativeMethods
            var scan = NativeMethods.VkKeyScan(character);
            if (scan == -1)
            {
                 // This can happen for characters not directly on the keyboard layout
                 // or requiring AltGr, which VkKeyScan doesn't fully handle by itself.
                Debug.WriteLine($"[SendCharAsync] VkKeyScan returned -1 for character: '{character}'. Attempting direct TextEntry as fallback for this char.");
                // Fallback for characters VkKeyScan can't handle (e.g. some special symbols or unicode chars)
                // This is not ideal if TextEntry is the root problem, but for single complex chars it might be different.
                // Or, consider logging an error and skipping the character.
                // For now, let's try TextEntry for this single char. If it fails, the issue is deeper.
                keyboard.TextEntry(character.ToString());
                await Task.Delay(interKeyDelayMs);
                return;
            }

            var vkCode = (VirtualKeyCode)(scan & 0xFF);
            var shiftNeeded = (scan & 0x0100) != 0;

            Debug.WriteLine($"Typing: '{character}' (VK: {vkCode}, Shift: {shiftNeeded})");

            if (shiftNeeded)
                keyboard.ModifiedKeyStroke(VirtualKeyCode.SHIFT, vkCode);
            else
                keyboard.KeyPress(vkCode);

            await Task.Delay(interKeyDelayMs);
        }

        private static async Task TypePasswordAsync(
            IKeyboardSimulator keyboard,
            string password)
        {
            Debug.WriteLine($"[TypePasswordAsync] Typing password: {password}");

            foreach (var c in password)
                await SendCharAsync(keyboard, c);
        }


        
        private async Task WaitForClientReady(Process gameProc, int timeoutMs = 10000)
        {
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (gameProc.HasExited)
                    throw new Exception("Client exited before login finished.");

                gameProc.Refresh();
                var title = gameProc.MainWindowTitle;

                // Adjust this condition to match when the game is *done* logging in.
                // If the title changes or a window handle appears, you can check for that here.
                if (!string.IsNullOrWhiteSpace(title) && !title.Contains("Unora"))
                {
                    return; // Assume client reached post-login state
                }

                await Task.Delay(500);
            }

            throw new TimeoutException("Client did not appear ready within timeout.");
        }
        
        
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BlockInput(bool fBlockIt);
        
        
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
            RegisterGlobalHotkeys(); // Register hotkeys after initial load & update checks
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

            // MainWindow properties (UseDawndWindower, UseLocalhost, SkipIntro)
            // are now getters/setters directly manipulating _launcherSettings.
            // No need to update them separately here after _launcherSettings is assigned.
            // Theme is applied via ApplySettings or App.ChangeTheme.
        }

        
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            UnregisterGlobalHotkeys(); // Clean up hotkeys
            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource.Dispose();
                _hwndSource = null;
            }

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