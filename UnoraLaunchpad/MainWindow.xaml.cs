using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Newtonsoft.Json;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace UnoraLaunchpad;

public sealed partial class MainWindow
{
    private static readonly string LocalDirPath = AppDomain.CurrentDomain.BaseDirectory;
    private static readonly string LauncherSettings = AppDomain.CurrentDomain.BaseDirectory + "\\LauncherSettings\\settings.json";
    private readonly FileService _fileService = new();
    private readonly HttpService _httpService = new();
    private NotifyIcon _notifyIcon;
    public AppSettings AppSettings { get; set; }

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

        // Load app settings from embedded resources
        AppSettings = LoadAppSettingsFromResources();
    }

    public void ApplySettings()
    {
        var settings = _fileService.LoadSettings(LauncherSettings);
        UseDawndWindower = settings.UseDawndWindower;
        UseLocalhost = settings.UseLocalhost;
        SkipIntro = settings.SkipIntro;
    }

    private string CalculateHash(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hash = md5.ComputeHash(stream);

        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private async Task CheckForFileUpdates()
    {
        ApplySettings();
        LaunchBtn.IsEnabled = false;
        EnsureUnoraFolderExists();

        var additionalFolders = new List<string>
        {
            Path.Combine("Unora", "maps"),
            Path.Combine("Unora", "npc")
        };

        var allFilesUrl = AppSettings.BaseUrl + AppSettings.ApiBaseAllFilesUrl;
        var downloadUrl = AppSettings.BaseUrl + AppSettings.DownloadUrl;

        ProgressLabel.Content = "Checking for updated files on the server. Please wait...";

        var serverFiles = await _httpService.GetFilesWithHashes(allFilesUrl);
        var localFiles = GetLocalFilesWithHashes();

        var filesToUpdate = new List<FileDetail>();

        foreach (var serverFile in serverFiles)
        {
            var updateRequired = false;

            if (!localFiles.TryGetValue(serverFile.FileName, out var file))
                updateRequired = true;
            else if (file != serverFile.Hash)
                updateRequired = true;
            else if (additionalFolders.Any(folder => IsFileInFolder(localFiles, folder, serverFile)))
                updateRequired = true;

            if (updateRequired)
                filesToUpdate.Add(serverFile);
        }

        if (filesToUpdate.Count == 0)
        {
            ProgressLabel.Content = "All files are up to date.";
            LaunchBtn.IsEnabled = true;

            return;
        }

        var tasks = filesToUpdate.Select(
                                     fileToUpdate =>
                                     {
                                         var downloadPath = Path.Combine(LocalDirPath, "Unora", fileToUpdate.FileName);

                                         foreach (var folder in additionalFolders)
                                             if (fileToUpdate.FileName.StartsWith(folder, StringComparison.Ordinal))
                                             {
                                                 downloadPath = Path.Combine(LocalDirPath, fileToUpdate.FileName);

                                                 break;
                                             }

                                         return FileService.DownloadFileFromServer(
                                             downloadUrl,
                                             fileToUpdate.FileName,
                                             downloadPath,
                                             new Progress<long>(
                                                 _ =>
                                                 {
                                                     ProgressLabel.Content = $"Updating {fileToUpdate.FileName}";
                                                 }));
                                     })
                                 .ToList();

        await Task.WhenAll(tasks);
        ProgressLabel.Content = "Update complete.";
        LaunchBtn.IsEnabled = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void CogButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow();
        settingsWindow.Show();
    }

    private void EnsureUnoraFolderExists()
    {
        var unoraFolderPath = Path.Combine(LocalDirPath, "Unora");

        if (!Directory.Exists(unoraFolderPath))
            Directory.CreateDirectory(unoraFolderPath);
    }

    private Dictionary<string, string> GetLocalFilesWithHashes()
    {
        // Specify the directory path
        var baseDir = Path.Combine(LocalDirPath, "Unora");
        var files = Directory.GetFiles(baseDir, "*", SearchOption.AllDirectories);

        var fileHashes = new ConcurrentDictionary<string, string>();

        // Use parallel processing to compute hashes for all files
        Parallel.ForEach(
            files,
            file =>
            {
                try
                {
                    // Calculate the relative path from the base directory to the file
                    var relativePath = GetRelativePath(baseDir, file);

                    // Calculate the hash of the file content
                    var hash = CalculateHash(file);

                    // Add or update the hash in the concurrent dictionary
                    fileHashes[relativePath] = hash;
                } catch (Exception ex)
                {
                    // Log the error for the problematic file
                    Debug.WriteLine($"Error processing file {file}: {ex.Message}");
                }
            });

        // Convert the concurrent dictionary to a regular dictionary
        return fileHashes.ToDictionary(k => k.Key, v => v.Value);
    }

    private string GetRelativePath(string basePath, string filePath)
    {
        var fileUri = new Uri(filePath);
        var baseUri = new Uri(basePath);

        if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            baseUri = new Uri(basePath + Path.DirectorySeparatorChar);

        return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fileUri).ToString().Replace('/', Path.DirectorySeparatorChar));
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

    private bool IsFileInFolder(IReadOnlyDictionary<string, string> localFiles, string folder, FileDetail serverFile) =>
        // Check if any of the local files' paths contain the folder name and the file name
        localFiles.Keys.Any(localFile => localFile.Contains(Path.Combine(folder, serverFile.FileName)));

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
            ipAddress = ResolveHostname("unoralauncher.duckdns.org");
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

    private void LaunchBtn_OnClick(object sender, RoutedEventArgs e)
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
            ipAddress = ResolveHostname("unoralauncher.duckdns.org");
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
            await Application.Current.Dispatcher.Invoke(async () => await CheckForFileUpdates());
        } catch (Exception ex)
        {
            LogException(ex);
        }
    }

    public async Task LoadAndBindGameUpdates()
    {
        var gameUpdatesUrl = AppSettings.BaseUrl + AppSettings.LauncherUpdatesUrl;
        var gameUpdates = await _httpService.LoadGameUpdatesAsync(gameUpdatesUrl);
        GameUpdatesControl.DataContext = new { GameUpdates = gameUpdates };
    }

    public static AppSettings LoadAppSettings(string path)
    {
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);

            return JsonConvert.DeserializeObject<AppSettings>(json);
        }

        return new AppSettings();
    }

    private AppSettings LoadAppSettingsFromResources()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string RESOURCE_NAME = "UnoraLaunchpad.Resources.appsettings.json";

        using var stream = assembly.GetManifestResourceStream(RESOURCE_NAME);

        if (stream != null)
        {
            using var reader = new StreamReader(stream);

            var json = reader.ReadToEnd();

            return JsonConvert.DeserializeObject<AppSettings>(json);
        }

        return null;
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

    public void SaveSettings(Settings settings) => _fileService.SaveSettings(settings, LauncherSettings);

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