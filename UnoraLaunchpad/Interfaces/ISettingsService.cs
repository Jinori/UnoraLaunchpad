using System;
using System.Windows; // For WindowState and Uri (Uri might be System)
using UnoraLaunchpad.Definitions; // For Settings

namespace UnoraLaunchpad.Interfaces
{
    public interface ISettingsService
    {
        Settings GetCurrentSettings();
        void SaveSettings(Settings settings);
        void LoadAndApplyTheme(Action<Uri> themeChanger);
        void LoadAndApplyWindowDimensions(Action<double, double, double, double> dimensionApplier);
        void UpdateAndSaveWindowBounds(double width, double height, double left, double top, WindowState windowState);
    }
}
