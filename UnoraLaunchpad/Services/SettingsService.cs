using System;
using System.IO;
using System.Windows; // Required for SystemParameters
using UnoraLaunchpad.Models;    // Required for Settings class
using UnoraLaunchpad.Interfaces; // Required for ISettingsService
// UnoraLaunchpad.Definitions is not directly used in this file for CONSTANTS, so it can be removed if only for Settings.

namespace UnoraLaunchpad.Services
{
    /// <summary>
    /// Manages the loading, saving, and application of launcher settings.
    /// This includes UI theme, window dimensions, and game-specific settings.
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private static readonly string LauncherSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LauncherSettings", "settings.json");
        private readonly FileService _fileService;
        private Settings _currentSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsService"/> class.
        /// Loads existing settings from disk or creates default settings if none are found.
        /// </summary>
        /// <param name="fileService">The file service instance for loading and saving settings.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="fileService"/> is null.</exception>
        public SettingsService(FileService fileService)
        {
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            LoadSettings();
        }

        /// <summary>
        /// Gets a copy of the current launcher settings.
        /// </summary>
        /// <returns>A <see cref="Settings"/> object representing the current settings. Returns default settings if none are loaded.</returns>
        /// <remarks>
        /// Returns a new instance of <see cref="Settings"/> to prevent external modification of the internal state.
        /// </remarks>
        public Settings GetCurrentSettings()
        {
            // Return a copy to prevent external modification if Settings is a class
            // If Settings is a struct, direct return is fine. Assuming it's a class for now.
            return _currentSettings != null ? new Settings(_currentSettings) : new Settings();
        }

        /// <summary>
        /// Loads settings from the predefined path or initializes with default settings if the file doesn't exist or is invalid.
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                _currentSettings = _fileService.LoadSettings(LauncherSettingsPath);
            }
            catch (Exception ex)
            {
                // Log error from FileService.LoadSettings
                Utils.LoggingService.LogError($"Error loading settings from '{LauncherSettingsPath}'. Initializing with default settings. Error: {ex.Message}", ex);
                _currentSettings = null; // Ensure it's null so defaults are applied
            }

            if (_currentSettings == null)
            {
                Utils.LoggingService.LogInfo($"No settings file found or settings were invalid at '{LauncherSettingsPath}'. Creating new default settings.");
                _currentSettings = new Settings(); // Fallback to default settings
                // Ensure default theme is set if settings were null or didn't have one
                if (string.IsNullOrEmpty(_currentSettings.SelectedTheme))
                {
                    _currentSettings.SelectedTheme = "Dark";
                }
                // Save the newly created default settings
                SaveSettingsInternal();
            }
        }

        /// <summary>
        /// Ensures that the settings are loaded. If not, it calls LoadSettings().
        /// </summary>
        private void EnsureSettingsLoaded()
        {
            if (_currentSettings == null)
            {
                LoadSettings();
            }
        }

        /// <summary>
        /// Saves the provided settings to disk and updates the internal current settings cache.
        /// </summary>
        /// <param name="settings">The <see cref="Settings"/> object to save.</param>
        public void SaveSettings(Settings settings)
        {
            _currentSettings = settings;
            SaveSettingsInternal();
        }

        /// <summary>
        /// Internal method to save the current state of <see cref="_currentSettings"/> to disk.
        /// </summary>
        private void SaveSettingsInternal()
        {
            if (_currentSettings != null)
            {
                try
                {
                    _fileService.SaveSettings(_currentSettings, LauncherSettingsPath);
                }
                catch (Exception ex)
                {
                    // Log error from FileService.SaveSettings
                    Utils.LoggingService.LogError($"Error saving settings to '{LauncherSettingsPath}'. Error: {ex.Message}", ex);
                    // Depending on policy, might re-throw or notify user. For now, just log.
                    // Re-throwing would make the operation fail more obviously to the caller.
                    throw;
                }
            }
        }

        /// <summary>
        /// Loads the selected theme from settings and applies it using the provided action.
        /// </summary>
        /// <param name="themeChanger">An action that takes a <see cref="Uri"/> pointing to the theme resource dictionary and applies it.</param>
        public void LoadAndApplyTheme(Action<Uri> themeChanger)
        {
            EnsureSettingsLoaded();

            var themeName = _currentSettings.SelectedTheme;
            if (string.IsNullOrEmpty(themeName))
            {
                themeName = "Dark"; // Default theme
            }

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
                default: // Dark
                    themeUri = new Uri("pack://application:,,,/Resources/DarkTheme.xaml", UriKind.Absolute);
                    break;
            }
            themeChanger?.Invoke(themeUri);
        }

        /// <summary>
        /// Loads window dimensions (width, height, left, top) from settings and applies them using the provided action.
        /// Ensures that the window is placed mostly on screen.
        /// </summary>
        /// <param name="dimensionApplier">
        /// An action that takes the loaded width, height, left, and top position and applies them to the window.
        /// Parameters are: width, height, newLeft, newTop.
        /// </param>
        public void LoadAndApplyWindowDimensions(Action<double, double, double, double> dimensionApplier)
        {
            EnsureSettingsLoaded();

            if (_currentSettings.WindowWidth > 0 && _currentSettings.WindowHeight > 0)
            {
                // Ensure the window is placed mostly on screen.
                double actualWidth = _currentSettings.WindowWidth;
                double actualHeight = _currentSettings.WindowHeight;
                double newLeft = _currentSettings.WindowLeft;
                double newTop = _currentSettings.WindowTop;

                // Avoid applying if both Left and Top are 0, as this might be an uninitialized state
                // or could make the window appear at an awkward default position for some systems.
                if (_currentSettings.WindowLeft != 0 || _currentSettings.WindowTop != 0)
                {
                    double maxLeft = SystemParameters.VirtualScreenWidth - actualWidth;
                    double maxTop = SystemParameters.VirtualScreenHeight - actualHeight;

                    newLeft = Math.Min(Math.Max(0, _currentSettings.WindowLeft), maxLeft);
                    newTop = Math.Min(Math.Max(0, _currentSettings.WindowTop), maxTop);
                }
                dimensionApplier?.Invoke(actualWidth, actualHeight, newLeft, newTop);
            }
        }

        /// <summary>
        /// Updates the stored window bounds (width, height, left, top) based on the current window state and saves them.
        /// Only saves dimensions if the window is in a <see cref="WindowState.Normal"/> state.
        /// </summary>
        /// <param name="width">The current width of the window.</param>
        /// <param name="height">The current height of the window.</param>
        /// <param name="left">The current left position of the window.</param>
        /// <param name="top">The current top position of the window.</param>
        /// <param name="windowState">The current <see cref="WindowState"/> of the window.</param>
        public void UpdateAndSaveWindowBounds(double width, double height, double left, double top, WindowState windowState)
        {
            EnsureSettingsLoaded();

            // Only save size and position if the window is in its normal state
            if (windowState == WindowState.Normal)
            {
                _currentSettings.WindowWidth = width;
                _currentSettings.WindowHeight = height;
                _currentSettings.WindowTop = top;
                _currentSettings.WindowLeft = left;
            }
            SaveSettingsInternal();
        }
    }
}
