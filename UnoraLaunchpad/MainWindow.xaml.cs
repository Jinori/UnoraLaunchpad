using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Newtonsoft.Json;

namespace UnoraLaunchpad;

public sealed partial class MainWindow
{
    private static readonly string LocalDirPath = AppDomain.CurrentDomain.BaseDirectory;
    private static readonly string LauncherSettings = AppDomain.CurrentDomain.BaseDirectory + "\\LauncherSettings\\settings.json";

    private readonly FileService _fileService = new();
    private readonly HttpService _httpService = new();
    public AppSettings AppSettings { get; set; }

    public ObservableCollection<GameUpdate> GameUpdates { get; set; } = new();
    public bool UseDawndWindower { get; set; }
    public bool UseLocalhost { get; set; }
    public ICommand OpenGameUpdateCommand { get; }
    public object Sync { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
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
    }

    private string CalculateHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        var bytes = File.ReadAllBytes(filePath);
        var hashBytes = sha256.ComputeHash(bytes);

        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    private async Task CheckForFileUpdates()
    {
        ApplySettings();
        LaunchBtn.IsEnabled = false;

        // List of folders containing files to be checked
        var additionalFolders = new List<string>
        {
            "maps",
            "npc"
            // Add more folder paths as needed
        };

        // Combine the base URL with the API endpoint for all files
        var allFilesUrl = AppSettings.BaseUrl + AppSettings.ApiBaseAllFilesUrl;

        // Combine the base URL with the download endpoint
        var downloadUrl = AppSettings.BaseUrl + AppSettings.DownloadUrl;

        ProgressLabel.Content = "Checking for updated files on the server. Please wait...";

        var serverFiles = await _httpService.GetFilesWithHashes(allFilesUrl);
        var localFiles = GetLocalFilesWithHashes();

        var filesToUpdate = serverFiles.Where(
                                           serverFile =>
                                               !localFiles.ContainsKey(serverFile.FileName)
                                               || (localFiles[serverFile.FileName] != serverFile.Hash)
                                               || additionalFolders.Any(folder => IsFileInFolder(localFiles, folder, serverFile)))
                                       .ToList();

        foreach (var fileToUpdate in filesToUpdate)
        {
            ProgressLabel.Content = $"Downloading {fileToUpdate.FileName}...";
            ProgressBar.Value = 0;

            // Download the file based on the download URL and file name
            await FileService.DownloadFileFromServer(
                downloadUrl,
                fileToUpdate.FileName,
                Path.Combine(LocalDirPath, fileToUpdate.FileName),
                new Progress<long>(value => ProgressBar.Value = value));

            ProgressBar.Value = 0;
        }

        ProgressLabel.Content = "Update complete.";
        LaunchBtn.IsEnabled = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void CogButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow();
        settingsWindow.Show();
    }

    private Dictionary<string, string> GetLocalFilesWithHashes()
    {
        var files = Directory.GetFiles(LocalDirPath, "*", SearchOption.AllDirectories);
        var fileHashes = new ConcurrentDictionary<string, string>();

        Parallel.ForEach(
            files,
            file =>
            {
                var relativePath = GetRelativePath(LocalDirPath, file);
                var hash = CalculateHash(file);
                fileHashes[relativePath] = hash;
            });

        return fileHashes.ToDictionary(k => k.Key, v => v.Value);
    }

    private string GetRelativePath(string basePath, string filePath)
    {
        if (filePath.StartsWith(basePath, StringComparison.Ordinal))
            return filePath.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar);

        return filePath;
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

        //retreive function pointer for remote thread
        var injectionPtr = NativeMethods.GetProcAddress(NativeMethods.GetModuleHandle("kernel32.dll"), "LoadLibraryA");

        //if failed to retreive function pointer
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

        using var process = SuspendedProcess.Start(AppDomain.CurrentDomain.BaseDirectory + "\\Darkages.exe");

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

    private void OpenGameUpdate(GameUpdate gameUpdate)
    {
        var detailView = new GameUpdateDetailView(gameUpdate);
        detailView.ShowDialog(); // Opens the detail view window
    }

    private static void PatchClient(SuspendedProcess process, IPAddress serverIPAddress, int serverPort)
    {
        using var stream = new ProcessMemoryStream(process.ProcessId);

        using var patcher = new RuntimePatcher(ClientVersion.Version741, stream, true);

        patcher.ApplyServerHostnamePatch(serverIPAddress);
        patcher.ApplyServerPortPatch(serverPort);
        //patcher.ApplySkipIntroVideoPatch();
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

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}