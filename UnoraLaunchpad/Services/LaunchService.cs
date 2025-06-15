using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows; // For MessageBox if UserNotifierService is not used directly for all messages
using UnoraLaunchpad.Definitions; // For Settings, SuspendedProcess, etc.
using UnoraLaunchpad.Interfaces; // Required for ILaunchService
using UnoraLaunchpad.Utils;     // For NativeMethods if still used directly by any moved logic, LoggingService

namespace UnoraLaunchpad.Services
{
    /// <summary>
    /// Handles the process of launching the selected game.
    /// This includes resolving server information, patching the game client in memory,
    /// injecting necessary DLLs (like DawndWindower), and renaming the game window.
    /// </summary>
    public class LaunchService : ILaunchService
    {
        private readonly ISettingsService _settingsService;
        private readonly IUserNotifierService _userNotifierService;

        /// <summary>
        /// Gets the current settings from the SettingsService.
        /// </summary>
        private Settings CurrentSettings => _settingsService.GetCurrentSettings();

        /// <summary>
        /// Initializes a new instance of the <see cref="LaunchService"/> class.
        /// </summary>
        /// <param name="settingsService">The service for accessing launcher and game settings.</param>
        /// <param name="userNotifierService">The service for notifying the user of events or errors.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="settingsService"/> or <paramref name="userNotifierService"/> is null.</exception>
        public LaunchService(ISettingsService settingsService, IUserNotifierService userNotifierService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _userNotifierService = userNotifierService ?? throw new ArgumentNullException(nameof(userNotifierService));
        }

        /// <summary>
        /// Gets the game-specific folder name and executable name.
        /// </summary>
        /// <param name="selectedGame">The name of the selected game.</param>
        /// <returns>A tuple containing the game folder and executable name. Defaults to "Unora" if the game is not recognized.</returns>
        private (string folder, string exe) GetGameLaunchInfo(string selectedGame)
        {
            return selectedGame switch
            {
                "Unora"   => ("Unora", "Unora.exe"),
                "Legends" => ("Legends", "Client.exe"),
                _         => ("Unora", "Unora.exe")
            };
        }

        /// <summary>
        /// Resolves a hostname to an IPv4 address.
        /// </summary>
        /// <param name="hostname">The hostname to resolve.</param>
        /// <returns>The first resolved IPv4 address, or <c>null</c> if resolution fails or no IPv4 address is found.</returns>
        private static IPAddress ResolveHostname(string hostname)
        {
            try
            {
                var hostEntry = Dns.GetHostEntry(hostname);
                var ipAddresses = hostEntry.AddressList
                    .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                    .ToList();

                if (!ipAddresses.Any())
                {
                    Debug.WriteLine($"[LaunchService] No IPv4 address found for {hostname}.");
                    return null;
                }
                return ipAddresses.First();
            }
            catch (SocketException ex)
            {
                Debug.WriteLine($"[LaunchService] DNS resolution failed for {hostname}: {ex.Message}");
                LoggingService.LogWarning($"DNS resolution failed for {hostname}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the IP address and port for the game server connection based on current settings.
        /// </summary>
        /// <returns>A tuple containing the server's <see cref="IPAddress"/> and port. IPAddress may be null if resolution fails.</returns>
        private (IPAddress IPAddress, int Port) GetServerConnection()
        {
            if (CurrentSettings.UseLocalhost)
                return (ResolveHostname("127.0.0.1"), 4200);

            return (ResolveHostname("chaotic-minds.dynu.net"), 6900);
        }

        /// <summary>
        /// Patches the game client in memory with server details and other modifications.
        /// </summary>
        /// <param name="process">The game process, started in a suspended state.</param>
        /// <param name="serverIPAddress">The IP address of the game server.</param>
        /// <param name="serverPort">The port of the game server.</param>
        /// <exception cref="InvalidOperationException">Thrown if <paramref name="serverIPAddress"/> is null, indicating a resolution failure.</exception>
        private void PatchClient(SuspendedProcess process, IPAddress serverIPAddress, int serverPort)
        {
            // Use CurrentSettings property
            using var stream = new ProcessMemoryStream(process.ProcessId);
            using var patcher = new RuntimePatcher(ClientVersion.Version741, stream, true); // Assumes ClientVersion.Version741 is accessible

            if (serverIPAddress == null)
            {
                Debug.WriteLine("[LaunchService] Server IP address is null. Cannot apply hostname patch.");
                throw new InvalidOperationException("Server IP address resolution failed and is required for patching.");
            }
            patcher.ApplyServerHostnamePatch(serverIPAddress);
            patcher.ApplyServerPortPatch(serverPort);

            if (CurrentSettings.SkipIntro)
                patcher.ApplySkipIntroVideoPatch();

            patcher.ApplyMultipleInstancesPatch();
        }

        /// <summary>
        /// Injects a DLL into the specified process.
        /// </summary>
        /// <param name="processHandle">A handle to the target process with necessary access rights.</param>
        private void InjectDll(IntPtr processHandle)
        {
            const string DLL_NAME = "dawnd.dll";
            var nameLength = DLL_NAME.Length + 1;

            IntPtr allocate = NativeMethods.VirtualAllocEx(processHandle, IntPtr.Zero, (IntPtr)nameLength, 0x1000, 0x40);
            if (allocate == IntPtr.Zero)
            {
                LoggingService.LogError("DLL Injection: Failed to allocate memory in target process.", null);
                _userNotifierService.ShowError("Failed to allocate memory in target process for DLL injection.", "Injection Error");
                return;
            }

            if (!NativeMethods.WriteProcessMemory(processHandle, allocate, DLL_NAME, (UIntPtr)nameLength, out _))
            {
                LoggingService.LogError("DLL Injection: Failed to write DLL name to target process memory.", null);
                _userNotifierService.ShowError("Failed to write DLL name to target process memory.", "Injection Error");
                NativeMethods.VirtualFreeEx(processHandle, allocate, UIntPtr.Zero, 0x8000);
                return;
            }

            IntPtr injectionPtr = NativeMethods.GetProcAddress(NativeMethods.GetModuleHandle("kernel32.dll"), "LoadLibraryA");
            if (injectionPtr == IntPtr.Zero)
            {
                LoggingService.LogError("DLL Injection: Could not find LoadLibraryA address.", null);
                _userNotifierService.ShowError("Could not find LoadLibraryA address. Injection failed.", "Injection Error");
                NativeMethods.VirtualFreeEx(processHandle, allocate, UIntPtr.Zero, 0x8000);
                return;
            }

            IntPtr thread = NativeMethods.CreateRemoteThread(processHandle, IntPtr.Zero, IntPtr.Zero, injectionPtr, allocate, 0, out _);
            if (thread == IntPtr.Zero)
            {
                LoggingService.LogError("DLL Injection: Failed to create remote thread in target process.", null);
                _userNotifierService.ShowError("Failed to create remote thread in target process. Injection failed.", "Injection Error");
                NativeMethods.VirtualFreeEx(processHandle, allocate, UIntPtr.Zero, 0x8000);
                return;
            }

            uint waitResult = NativeMethods.WaitForSingleObject(thread, 10 * 1000);
            NativeMethods.CloseHandle(thread);

            if (waitResult != 0x00000000L) // WAIT_OBJECT_0
            {
                LoggingService.LogWarning($"DLL injection thread did not signal success as expected or timed out. Result: {waitResult}");
                // Notifying user might be too much here if previous errors already shown, or could be a specific timeout message.
                // For now, just logging it as a warning as primary failures are already handled.
            }
            NativeMethods.VirtualFreeEx(processHandle, allocate, UIntPtr.Zero, 0x8000);
        }

        /// <summary>
        /// Asynchronously attempts to rename the main window of the specified process.
        /// </summary>
        /// <param name="process">The process whose window needs renaming.</param>
        /// <param name="newTitle">The new title for the window.</param>
        private async Task RenameGameWindowAsync(Process process, string newTitle)
        {
            if (process == null || string.IsNullOrEmpty(newTitle)) return;

            for (var i = 0; i < 20; i++)
            {
                if (process.HasExited)
                {
                    Debug.WriteLine($"[LaunchService] Process exited before window could be renamed.");
                    break;
                }
                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    NativeMethods.SetWindowText(process.MainWindowHandle, newTitle);
                    Debug.WriteLine($"[LaunchService] Game window renamed to: {newTitle}");
                    break;
                }
                await Task.Delay(100);
            }
        }

        /// <summary>
        /// Asynchronously launches the selected game.
        /// This involves finding the game executable, resolving server connection,
        /// patching the client, optionally injecting DLLs, and then resuming the process.
        /// Errors are reported to the user via <see cref="UserNotifierService"/>.
        /// </summary>
        public async Task LaunchGameAsync()
        {
            // Use CurrentSettings property
            (var ipAddress, var serverPort) = GetServerConnection();

            if (ipAddress == null)
            {
                _userNotifierService.ShowError("Could not resolve server IP address. Please check your internet connection or DNS settings and try again.", "Launch Error");
                return;
            }

            var selectedGame = CurrentSettings?.SelectedGame ?? "Unora";
            (var gameFolder, var gameExe) = GetGameLaunchInfo(selectedGame);
            var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, gameFolder, gameExe);

            if (!File.Exists(exePath))
            {
                _userNotifierService.ShowError($"The game executable was not found: {exePath}", "Launch Error");
                LoggingService.LogError($"Game executable not found: {exePath} for game {selectedGame}");
                return;
            }

            try
            {
                Debug.WriteLine($"[LaunchService] Starting process: {exePath}");
                using var gameProcess = SuspendedProcess.Start(exePath);
                Debug.WriteLine($"[LaunchService] Process started in suspended state. PID: {gameProcess.ProcessId}");

                PatchClient(gameProcess, ipAddress, serverPort);
                Debug.WriteLine($"[LaunchService] Client patching attempted for PID: {gameProcess.ProcessId}");

                if (CurrentSettings.UseDawndWindower)
                {
                    Debug.WriteLine($"[LaunchService] Attempting DLL injection for PID: {gameProcess.ProcessId}");
                    IntPtr processHandle = NativeMethods.OpenProcess(
                        ProcessAccessFlags.CreateThread | ProcessAccessFlags.QueryInformation | ProcessAccessFlags.VirtualMemoryOperation | ProcessAccessFlags.VirtualMemoryRead | ProcessAccessFlags.VirtualMemoryWrite,
                        false,
                        gameProcess.ProcessId);

                    if (processHandle != IntPtr.Zero)
                    {
                        try { InjectDll(processHandle); }
                        finally { NativeMethods.CloseHandle(processHandle); }
                    }
                    else
                    {
                         LoggingService.LogError($"Failed to open process handle for PID: {gameProcess.ProcessId} with necessary rights for injection. Last Win32Error: {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}", null);
                         _userNotifierService.ShowError("Failed to open process with sufficient access for DLL injection.", "Injection Error");
                    }
                }

                Debug.WriteLine($"[LaunchService] Resuming process PID: {gameProcess.ProcessId}");
                gameProcess.Resume();

                Process runningProcess = null;
                try { runningProcess = Process.GetProcessById(gameProcess.ProcessId); }
                catch (ArgumentException ex)
                {
                    LoggingService.LogWarning($"Could not find process with ID {gameProcess.ProcessId} immediately after resume (for renaming). It might have exited. Error: {ex.Message}");
                }

                if (runningProcess != null)
                {
                   await RenameGameWindowAsync(runningProcess, selectedGame);
                }
            }
            catch (InvalidOperationException ioe) when (ioe.Message.Contains("Server IP address resolution failed"))
            {
                // This specific error is typically handled by the initial IP check or within PatchClient,
                // but if it somehow originates here, log as warning as user is likely already notified.
                LoggingService.LogWarning($"Launch aborted due to IP resolution failure during patching: {ioe.Message}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to launch game {selectedGame}: {ex.Message}", ex);
                _userNotifierService.ShowError($"An error occurred while launching the game. Please check logs for details.", "Launch Error");
            }
        }
    }
}
