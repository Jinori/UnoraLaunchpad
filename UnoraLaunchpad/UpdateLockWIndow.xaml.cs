using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace UnoraLaunchpad
{
    public partial class UpdateLockWindow : Window
    {
        public bool UserSkippedClosingClients { get; private set; } = false;
        private static readonly Dictionary<string, string> ThemeResourceMap = new()
        {
            { "Dark", "pack://application:,,,/Resources/DarkTheme.xaml" },
            { "Light", "pack://application:,,,/Resources/LightTheme.xaml" },
            { "Teal", "pack://application:,,,/Resources/TealTheme.xaml" },
            { "Violet", "pack://application:,,,/Resources/VioletTheme.xaml" },
            { "Amber", "pack://application:,,,/Resources/AmberTheme.xaml" },
            { "Emerald", "pack://application:,,,/Resources/EmeraldTheme.xaml" }
        };

        public UpdateLockWindow()
        {
            InitializeComponent();
            Loaded += UpdateLockWindow_Loaded;
        }

        private void UpdateLockWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Apply Theme
            var settingsPath = "LauncherSettings/settings.json";
            Settings launcherSettings = null;
            if (File.Exists(settingsPath))
                launcherSettings = FileService.LoadSettings(settingsPath);
            launcherSettings ??= new Settings();

            var themeName = launcherSettings.SelectedTheme;
            if (string.IsNullOrEmpty(themeName))
                themeName = "Dark"; // Default theme

            App.ChangeTheme(GetThemeUri(themeName));

            // Original Loaded logic
            UpdateStatus();
        }

        private static Uri GetThemeUri(string themeName)
        {
            if (!ThemeResourceMap.TryGetValue(themeName, out var uriString))
                uriString = ThemeResourceMap["Dark"]; // Default fallback
            return new Uri(uriString, UriKind.Absolute);
        }

        private void UpdateStatus()
        {
            var procs = Process.GetProcessesByName("Unora")
                               .ToArray();
            
            if (procs.Length == 0)
            {
                DialogResult = true; // allow update
                Close();
            }
            else
            {
                StatusText.Text = $"Found {procs.Length} game client(s) running. It's recommended to close them before updating, or you can choose to skip and continue (this may cause issues).";
            }
        }

        private void CheckAgainBtn_Click(object sender, RoutedEventArgs e) => UpdateStatus();

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();

            // Immediately close the entire launcher app
            Application.Current.Shutdown();
        }

        private void SkipBtn_Click(object sender, RoutedEventArgs e)
        {
            UserSkippedClosingClients = true;
            DialogResult = true;
            Close();
        }
    }
}