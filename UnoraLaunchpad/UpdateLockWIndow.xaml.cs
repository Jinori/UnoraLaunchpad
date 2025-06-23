using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            { "Emerald", "pack://application:,,,/Resources/EmeraldTheme.xaml" },
            { "Ruby", "pack://application:,,,/Resources/RubyTheme.xaml" },
            { "Sapphire", "pack://application:,,,/Resources/SapphireTheme.xaml" },
            { "Topaz", "pack://application:,,,/Resources/TopazTheme.xaml" },
            { "Amethyst", "pack://application:,,,/Resources/AmethystTheme.xaml" },
            { "Garnet", "pack://application:,,,/Resources/GarnetTheme.xaml" },
            { "Pearl", "pack://application:,,,/Resources/PearlTheme.xaml" },
            { "Obsidian", "pack://application:,,,/Resources/ObsidianTheme.xaml" },
            { "Citrine", "pack://application:,,,/Resources/CitrineTheme.xaml" },
            { "Peridot", "pack://application:,,,/Resources/PeridotTheme.xaml" },
            { "Aquamarine", "pack://application:,,,/Resources/AquamarineTheme.xaml" }
        };

        public UpdateLockWindow()
        {
            InitializeComponent();
            Loaded += UpdateLockWindow_Loaded;
        }

        private void UpdateLockWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Theme should be inherited from the application context.
            // No need to load settings or change theme here.
            // App.Current.Resources should already have the correct theme.

            // Original Loaded logic
            UpdateStatus();
        }

        private static Uri GetThemeUri(string themeName)
        {
            // This method is kept for potential future use or as a fallback,
            // but UpdateLockWindow should primarily rely on the app-level theme.
            if (!ThemeResourceMap.TryGetValue(themeName, out var uriString) || string.IsNullOrEmpty(uriString))
                uriString = ThemeResourceMap["Dark"]; // Default fallback if themeName not in map or URI is invalid
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