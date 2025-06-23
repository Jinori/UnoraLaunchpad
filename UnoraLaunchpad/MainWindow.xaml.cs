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
        #region Fields and Properties

        /// <summary>
        /// Path to the launcher settings file.
        /// </summary>
        private static readonly string LauncherSettingsPath = "LauncherSettings/settings.json";

        /// <summary>
        /// Provides file-related services.
        /// </summary>
        private readonly FileService FileService = new();

        /// <summary>
        /// Provides Unora client-related services.
        /// </summary>
        private readonly UnoraClient UnoraClient = new();

        /// <summary>
        /// Stores the current launcher settings.
        /// </summary>
        private Settings _launcherSettings;

        /// <summary>
        /// The system tray icon for the launcher.
        /// </summary>
        private NotifyIcon NotifyIcon;

        /// <summary>
        /// Collection of game updates for binding to the UI.
        /// </summary>
        public ObservableCollection<GameUpdate> GameUpdates { get; } = new();

        /// <summary>
        /// Indicates whether to skip the intro.
        /// </summary>
        public bool SkipIntro { get; set; }

        /// <summary>
        /// Indicates whether to use Dawnd Windower.
        /// </summary>
        public bool UseDawndWindower { get; set; }

        /// <summary>
        /// Indicates whether to use localhost for connections.
        /// </summary>
        public bool UseLocalhost { get; set; }

        /// <summary>
        /// Command to open a game update detail view.
        /// </summary>
        public ICommand OpenGameUpdateCommand { get; }

        /// <summary>
        /// Synchronization object for thread safety.
        /// </summary>
        public object Sync { get; } = new();

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            InitializeTrayIcon();
            OpenGameUpdateCommand = new RelayCommand<GameUpdate>(OpenGameUpdate);
            DataContext = this;
        }

        #endregion

        #region Settings

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

        /// <summary>
        /// Refreshes the saved characters ComboBox.
        /// </summary>
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

        #endregion

        #region Tray Icon

        /// <summary>
        /// Initializes the system tray icon and menu.
        /// </summary>
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

        /// <summary>
        /// Handles the tray menu exit click event.
        /// </summary>
        private void TrayMenu_Exit_Click(object sender, EventArgs e)
        {
            NotifyIcon.Dispose();
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Handles the tray menu open click event.
        /// </summary>
        private void TrayMenu_Open_Click(object sender, EventArgs e) => ShowWindow();

        /// <summary>
        /// Shows the main window from the tray.
        /// </summary>
        private void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;
        }

        #endregion

        #region Game Updates

        /// <summary>
        /// Loads and binds game updates to the UI.
        /// </summary>
        public async Task LoadAndBindGameUpdates()
        {
            var apiRoutes = GetCurrentApiRoutes();
            var gameUpdates = await UnoraClient.GetGameUpdatesAsync(apiRoutes.GameUpdates);
            GameUpdatesControl.DataContext = new { GameUpdates = gameUpdates };
        }

        /// <summary>
        /// Opens the game update detail view.
        /// </summary>
        private void OpenGameUpdate(GameUpdate gameUpdate)
        {
            var detailView = new GameUpdateDetailView(gameUpdate);
            detailView.ShowDialog();
        }

        /// <summary>
        /// Handles the auto-login button click event.
        /// </summary>
        private async void LaunchSavedBtn_Click(object sender, RoutedEventArgs e)
        {
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
                                character.Password = decryptedPassword;
                                await LaunchAndLogin(character);
                                character.Password = null;
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
                else
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
                                characterToLaunch.Password = decryptedPassword;
                                await LaunchAndLogin(characterToLaunch);
                                characterToLaunch.Password = null;
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
                                           _launcherSettings.SavedCharacters.Any();
            }
        }

        /// <summary>
        /// Launches and logs in a character asynchronously.
        /// </summary>
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

        /// <summary>
        /// Performs automated login for a character.
        /// </summary>
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

        #endregion

        #region Launcher Core

        /// <summary>
        /// Gets the server connection IP and port.
        /// </summary>
        private (IPAddress, int) GetServerConnection()
        {
            if (UseLocalhost)
                return (ResolveHostname("127.0.0.1"), 4200);

            return (ResolveHostname("chaotic-minds.dynu.net"), 6900);
        }

        /// <summary>
        /// Resolves a hostname to an IP address.
        /// </summary>
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

        /// <summary>
        /// Patches the client process for launching.
        /// </summary>
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

        /// <summary>
        /// Injects a DLL into the process if needed.
        /// </summary>
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

        /// <summary>
        /// Renames the game window asynchronously.
        /// </summary>
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

        /// <summary>
        /// Sets the window title based on the selected game.
        /// </summary>
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

        /// <summary>
        /// Waits for the client process to be ready for login.
        /// </summary>
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

        /// <summary>
        /// Blocks or unblocks user input at the OS level.
        /// </summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BlockInput(bool fBlockIt);

        /// <summary>
        /// Sets the cursor position on the screen.
        /// </summary>
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        /// <summary>
        /// Simulates mouse events at the OS level.
        /// </summary>
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        private const uint MouseeventfLeftdown = 0x0002;
        private const uint MouseeventfLeftup = 0x0004;

        /// <summary>
        /// Moves the mouse to a point and simulates a left click.
        /// </summary>
        private void MoveAndClickPoint(System.Drawing.Point point)
        {
            SetCursorPos(point.X, point.Y);
            Thread.Sleep(100);
            mouse_event(MouseeventfLeftdown, (uint)point.X, (uint)point.Y, 0, 0);
            Thread.Sleep(50);
            mouse_event(MouseeventfLeftup, (uint)point.X, (uint)point.Y, 0, 0);
        }

        /// <summary>
        /// Gets a screen point relative to a window handle.
        /// </summary>
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

        #region Progress UI Helpers

        /// <summary>
        /// Sets the UI state to updating.
        /// </summary>
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

        /// <summary>
        /// Sets the UI state to idle.
        /// </summary>
        private void SetUiStateIdle() => Dispatcher.Invoke(() => LaunchBtn.IsEnabled = true);

        /// <summary>
        /// Sets the UI state to complete.
        /// </summary>
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

        /// <summary>
        /// Prepares the progress bar for updates.
        /// </summary>
        private void PrepareProgressBar(long totalBytesToDownload) =>
            Dispatcher.Invoke(() =>
            {
                ProgressFileName.Text = string.Empty;
                ProgressBytes.Text = "Applying updates...";
                DownloadProgressBar.IsIndeterminate = false;
                DownloadProgressBar.Minimum = 0;
                DownloadProgressBar.Maximum = totalBytesToDownload > 0 ? totalBytesToDownload : 1;
            });

        /// <summary>
        /// Prepares the file progress UI.
        /// </summary>
        private void PrepareFileProgress(string fileName, long downloaded, long total) =>
            Dispatcher.Invoke(() =>
            {
                ProgressFileName.Text = fileName;
                ProgressBytes.Text = $"{FormatBytes(downloaded)} of {FormatBytes(total)}";
                DownloadProgressBar.Value = downloaded;
            });

        /// <summary>
        /// Updates the file progress UI.
        /// </summary>
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

        /// <summary>
        /// Formats a byte value as a human-readable string.
        /// </summary>
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

        /// <summary>
        /// Formats a speed value as a human-readable string.
        /// </summary>
        private static string FormatSpeed(double bytesPerSec)
        {
            if (bytesPerSec > 1024 * 1024)
                return $"{bytesPerSec / (1024.0 * 1024.0):F2} MB/s";
            if (bytesPerSec > 1024)
                return $"{bytesPerSec / 1024.0:F2} KB/s";

            return $"{bytesPerSec:F2} B/s";
        }

        #endregion

        // --- Restored event handlers and logic methods for XAML/code references ---

        /// <summary>
        /// Handles the title bar mouse down event for window dragging.
        /// </summary>
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        /// <summary>
        /// Handles the Patch Notes button click event.
        /// </summary>
        private void PatchNotesButton_Click(object sender, RoutedEventArgs e)
        {
            var patchWindow = new PatchNotesWindow();
            patchWindow.Owner = this;
            patchWindow.ShowDialog();
        }

        /// <summary>
        /// Handles the Discord button click event.
        /// </summary>
        private void DiscordButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://discord.gg/WkqbMVvDJq",
                UseShellExecute = true
            });
        }

        /// <summary>
        /// Handles the settings (cog) button click event.
        /// </summary>
        private void CogButton_Click(object sender, RoutedEventArgs e)
        {
            if (_launcherSettings == null)
            {
                // Attempt to load or use defaults if ApplySettings hasn't run or failed
                try
                {
                    _launcherSettings = FileService.LoadSettings(LauncherSettingsPath);
                    if (_launcherSettings == null)
                    {
                        _launcherSettings = new Settings();
                    }
                }
                catch
                {
                    _launcherSettings = new Settings();
                }
            }
            var settingsWindow = new SettingsWindow(this, _launcherSettings);
            settingsWindow.Owner = this;
            settingsWindow.Show();
        }

        /// <summary>
        /// Handles the minimize button click event.
        /// </summary>
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// Handles the close button click event.
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Gets the game launch info (folder, exe) for the selected game.
        /// </summary>
        private (string folder, string exe) GetGameLaunchInfo(string selectedGame)
        {
            return selectedGame switch
            {
                "Unora" => ("Unora", "Unora.exe"),
                "Legends" => ("Legends", "Client.exe"),
                _ => ("Unora", "Unora.exe")
            };
        }

        /// <summary>
        /// Types a password asynchronously using a keyboard simulator.
        /// </summary>
        private static async Task TypePasswordAsync(InputSimulatorStandard.IKeyboardSimulator keyboard, string password)
        {
            foreach (var c in password)
            {
                var scan = VkKeyScan(c);
                if (scan == -1)
                {
                    Debug.WriteLine($"[SendCharAsync] Unsupported character: '{c}'");
                    continue;
                }
                var vkCode = (VirtualKeyCode)(scan & 0xFF);
                var shiftNeeded = (scan & 0x0100) != 0;
                Debug.WriteLine($"Typing: '{c}' (VK: {vkCode}, Shift: {shiftNeeded})");
                if (shiftNeeded)
                    keyboard.ModifiedKeyStroke(VirtualKeyCode.SHIFT, vkCode);
                else
                    keyboard.KeyPress(vkCode);
                await Task.Delay(50);
            }
        }

        /// <summary>
        /// Checks for file updates and updates the UI.
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
                    }
                    totalDownloaded += fileBytesDownloaded;
                }
            });
            Debug.WriteLine($"[Launcher] All updates completed.");
        }

        /// <summary>
        /// Checks and updates the launcher asynchronously.
        /// </summary>
        private async Task CheckAndUpdateLauncherAsync()
        {
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

        /// <summary>
        /// Reloads settings and refreshes the UI.
        /// </summary>
        public async void ReloadSettingsAndRefresh()
        {
            ApplySettings();
            SetWindowTitle();
            await LoadAndBindGameUpdates();
            await CheckForFileUpdates();
            Dispatcher.BeginInvoke(new Action(SetUiStateComplete));
        }
    }
}