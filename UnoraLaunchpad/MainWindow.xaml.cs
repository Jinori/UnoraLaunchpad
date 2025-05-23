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

namespace UnoraLaunchpad;

public sealed partial class MainWindow
{
    private static readonly string LauncherSettings = "LauncherSettings/settings.json";
    private readonly FileService FileService = new();
    private readonly UnoraClient UnoraClient = new();
    private NotifyIcon _notifyIcon;

    public ObservableCollection<GameUpdate> GameUpdates { get; set; } = new();
    public bool SkipIntro { get; set; }
    public bool UseDawndWindower { get; set; }
    public bool UseLocalhost { get; set; }
    public ICommand OpenGameUpdateCommand { get; }
    public object Sync { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        InitializeTrayIcon();
        OpenGameUpdateCommand = new RelayCommand<GameUpdate>(OpenGameUpdate);
        DataContext = this;
    }

    public void ApplySettings()
    {
        var settings = FileService.LoadSettings(LauncherSettings);
        UseDawndWindower = settings.UseDawndWindower;
        UseLocalhost = settings.UseLocalhost;
        SkipIntro = settings.SkipIntro;
    }

    private string CalculateHash(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hash = md5.ComputeHash(stream);

        return BitConverter.ToString(hash);
    }

    private async Task CheckForFileUpdates()
    {
        ApplySettings();
        LaunchBtn.IsEnabled = false;
        SwirlLoader.Visibility = Visibility.Visible;
        EnsureUnoraFolderExists();

        // Show the progress area, hide the status label
        Dispatcher.Invoke(() =>
        {
            DownloadProgressPanel.Visibility = Visibility.Visible;
            StatusLabel.Visibility = Visibility.Collapsed;
        });

        ProgressFileName.Text = "";
        ProgressBytes.Text = "Checking for updates...";
        ProgressSpeed.Text = "";
        DownloadProgressBar.IsIndeterminate = true;

        var fileDetails = await UnoraClient.GetFileDetailsAsync();

        // Find out which files actually need updating
        var filesToUpdate = fileDetails.Where(fileDetail =>
                                       {
                                           var filePath = Path.Combine(CONSTANTS.UNORA_FOLDER_NAME, fileDetail.RelativePath);

                                           if (!File.Exists(filePath))
                                               return true;

                                           var fileHash = CalculateHash(filePath);

                                           return !fileHash.Equals(fileDetail.Hash, StringComparison.OrdinalIgnoreCase);
                                       })
                                       .ToList();

        // --- NEW: Calculate the total bytes for all files being updated ---
        var totalBytesToDownload = filesToUpdate.Sum(f => f.Size); // Make sure f.Size is available and in bytes
        var totalDownloaded = 0L;

        if (filesToUpdate.Any())
        {
            var lockWindow = new UpdateLockWindow();
            var result = lockWindow.ShowDialog();

            if (result != true)
            {
                MessageBox.Show("Update cancelled. Please update later.", "Unora Launcher");
                LaunchBtn.IsEnabled = true;

                return;
            }
        }

        ProgressFileName.Text = "";
        ProgressBytes.Text = "Applying updates...";
        ProgressSpeed.Text = "";

        // --- Set the progress bar range to total size (use 0 if none, disables the bar) ---
        DownloadProgressBar.IsIndeterminate = false;
        DownloadProgressBar.Minimum = 0;
        DownloadProgressBar.Maximum = totalBytesToDownload > 0 ? totalBytesToDownload : 1; // fallback to 1 to avoid div/0

        await Task.Run(async () =>
        {
            foreach (var fileDetail in filesToUpdate)
            {
                var filePath = Path.Combine(CONSTANTS.UNORA_FOLDER_NAME, fileDetail.RelativePath);
                var directory = Path.GetDirectoryName(filePath)!;

                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                long fileBytesDownloaded = 0L;

                Dispatcher.Invoke(() =>
                {
                    ProgressFileName.Text = fileDetail.RelativePath;
                });

                var progress = new Progress<UnoraClient.DownloadProgress>(p =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        fileBytesDownloaded = p.BytesReceived;
                        ProgressBytes.Text = $"{FormatBytes(totalDownloaded + p.BytesReceived)} of {FormatBytes(totalBytesToDownload)}";
                        ProgressSpeed.Text = $"@ {FormatSpeed(p.SpeedBytesPerSec)}";
                        DownloadProgressBar.Value = totalDownloaded + p.BytesReceived;
                    });
                });

                await UnoraClient.DownloadFileAsync(fileDetail.RelativePath, filePath, progress);
                totalDownloaded += fileBytesDownloaded;
            }

            // When done, hide progress, show "Update complete."
            Dispatcher.BeginInvoke(() =>
            {
                DownloadProgressPanel.Visibility = Visibility.Collapsed;
                StatusLabel.Text = "Update complete.";
                StatusLabel.Visibility = Visibility.Visible;
                SwirlLoader.Visibility = Visibility.Collapsed;
                LaunchBtn.IsEnabled = true;
            });
        });
    }



    private static string FormatBytes(long bytes)
    {
        if (bytes < 0) return "??";
        if (bytes > 1024 * 1024)
            return $"{bytes / 1024 / 1024.0:F2} MB";
        if (bytes > 1024)
            return $"{bytes / 1024.0:F2} KB";

        return $"{bytes} B";
    }

    private static string FormatSpeed(double bytesPerSec)
    {
        if (bytesPerSec > 1024 * 1024)
            return $"{bytesPerSec / 1024 / 1024.0:F2} MB/s";
        if (bytesPerSec > 1024)
            return $"{bytesPerSec / 1024.0:F2} KB/s";

        return $"{bytesPerSec:F2} B/s";
    }


    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void CogButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow();
        settingsWindow.Show();
    }

    private void EnsureUnoraFolderExists()
    {
        if (!Directory.Exists(CONSTANTS.UNORA_FOLDER_NAME))
            Directory.CreateDirectory(CONSTANTS.UNORA_FOLDER_NAME);
    }
    
    private void InitializeTrayIcon()
    {
        // Create an icon from a resource
        var iconUri = new Uri("pack://application:,,,/UnoraLaunchpad;component/favicon.ico", UriKind.RelativeOrAbsolute);
        var iconStream = Application.GetResourceStream(iconUri)?.Stream;

        if (iconStream != null)
            _notifyIcon = new NotifyIcon
            {
                Icon = new Icon(iconStream),
                Visible = true
            };

        // Create a context menu for the tray icon
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Launch Client", null, Launch);
        contextMenu.Items.Add("Open Launcher", null, TrayMenu_Open_Click);
        contextMenu.Items.Add("Exit", null, TrayMenu_Exit_Click);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (_, _) => ShowWindow();
    }

    private void InjectDll(IntPtr accessHandle)
    {
        const string DLL_NAME = "dawnd.dll";

        //length of string containing the DLL file name +1 byte padding
        var nameLength = DLL_NAME.Length + 1;

        //allocate memory within the virtual address space of the target process
        var allocate = NativeMethods.VirtualAllocEx(
            accessHandle,
            (IntPtr)null,
            (IntPtr)nameLength,
            0x1000,
            0x40); //allocation pour WriteProcessMemory

        //write DLL file name to allocated memory in target process
        NativeMethods.WriteProcessMemory(
            accessHandle,
            allocate,
            DLL_NAME,
            (UIntPtr)nameLength,
            out _);

        var injectionPtr = NativeMethods.GetProcAddress(NativeMethods.GetModuleHandle("kernel32.dll"), "LoadLibraryA");

        if (injectionPtr == UIntPtr.Zero)
        {
            MessageBox.Show(this, "Injection pointer was null.", "Injection Error");

            //return failed
            return;
        }

        //create thread in target process, and store accessHandle in hThread
        var thread = NativeMethods.CreateRemoteThread(
            accessHandle,
            (IntPtr)null,
            IntPtr.Zero,
            injectionPtr,
            allocate,
            0,
            out _);

        //make sure thread accessHandle is valid
        if (thread == IntPtr.Zero)
        {
            //incorrect thread accessHandle ... return failed
            MessageBox.Show(this, "Remote injection thread was null. Try again...", "Injection Error");

            return;
        }

        //time-out is 10 seconds...
        var result = NativeMethods.WaitForSingleObject(thread, 10 * 1000);

        //check whether thread timed out...
        if (result != WaitEventResult.Signaled)
        {
            //thread timed out...
            MessageBox.Show(this, "Injection thread timed out, or signaled incorrectly. Try again...", "Injection Error");

            //make sure thread accessHandle is valid before closing... prevents crashes.
            if (thread != IntPtr.Zero)
                //close thread in target process
                NativeMethods.CloseHandle(thread);

            return;
        }

        //free up allocated space ( AllocMem )
        NativeMethods.VirtualFreeEx(
            accessHandle,
            allocate,
            (UIntPtr)0,
            0x8000);

        //make sure thread accessHandle is valid before closing... prevents crashes.
        if (thread != IntPtr.Zero)
            //close thread in target process
            NativeMethods.CloseHandle(thread);

        //return succeeded
    }
    
    private void Launch(object sender, EventArgs e)
    {
        IPAddress ipAddress;
        int serverPort;

        if (UseLocalhost)
        {
            ipAddress = ResolveHostname("127.0.0.1");
            serverPort = 4200;
        }
        else
        {
            ipAddress = ResolveHostname("chaotic-minds.dynu.net");
            serverPort = 6900;
        }

        using var process = SuspendedProcess.Start(AppDomain.CurrentDomain.BaseDirectory + "\\Unora\\" + "\\Darkages.exe");

        try
        {
            PatchClient(process, ipAddress, serverPort);

            if (UseDawndWindower)
            {
                var processPtr = NativeMethods.OpenProcess(ProcessAccessFlags.FullAccess, true, process.ProcessId);
                InjectDll(processPtr);
            }
        } catch (Exception ex)
        {
            // An error occured trying to patch the client
            Debug.WriteLine($"UnableToPatchClient: {ex.Message}");
        }
    }
    
    private async void Launcher_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await LoadAndBindGameUpdates();
            await CheckForFileUpdates();
        } catch (Exception ex)
        {
            LogException(ex);
        }
    }

    public async Task LoadAndBindGameUpdates()
    {
        var gameUpdates = await UnoraClient.GetGameUpdatesAsync();
        GameUpdatesControl.DataContext = new { GameUpdates = gameUpdates };
    }

    public static void LogException(Exception e)
    {
        var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LauncherSettings", "log.txt");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"{DateTime.Now}: {e}\n");
        } catch
        {
            // Handle any exceptions that might occur while logging
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
            Hide();

        base.OnStateChanged(e);
    }

    private void OpenGameUpdate(GameUpdate gameUpdate)
    {
        var detailView = new GameUpdateDetailView(gameUpdate);
        detailView.ShowDialog(); // Opens the detail view window
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

    public void SaveSettings(Settings settings) => FileService.SaveSettings(settings, LauncherSettings);


    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void TrayMenu_Exit_Click(object sender, EventArgs e)
    {
        _notifyIcon.Dispose();
        Application.Current.Shutdown();
    }

    private void TrayMenu_Open_Click(object sender, EventArgs e) => ShowWindow();
}