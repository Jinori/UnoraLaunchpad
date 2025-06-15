using System;
using System.Threading.Tasks;
// No specific "Definitions" needed here unless callbacks use custom types from there.

namespace UnoraLaunchpad.Interfaces
{
    public interface IUpdateService
    {
        string GetLocalLauncherVersion();
        Task CheckAndUpdateLauncherAsync(Action shutdownApplication);
        string GetFilePath(string relativePath);
        void EnsureDirectoryExists(string filePath);
        Task CheckForFileUpdatesAsync();

        // Callbacks for UI updates from MainWindow
        Action<long> PrepareProgressBarAction { get; set; }
        Action<string, long, long> PrepareFileProgressAction { get; set; }
        Action<long, long, long, double> UpdateFileProgressAction { get; set; }
        Action SetUiStateIdleAction { get; set; }
    }
}
