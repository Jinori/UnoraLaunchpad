using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using UnoraLaunchpad.Models; // Required for Settings

namespace UnoraLaunchpad
{
    public sealed partial class App : Application

    {
        public static void ChangeTheme(Uri themeUri)
        {
            // Find and remove existing theme dictionary if any
            var existingThemeDictionary = Current.Resources.MergedDictionaries
                                                 .FirstOrDefault(d =>
                                                     d.Source != null
                                                     && (d.Source.ToString().EndsWith("DarkTheme.xaml")
                                                         || d.Source.ToString().EndsWith("LightTheme.xaml")));

            if (existingThemeDictionary != null)
            {
                Current.Resources.MergedDictionaries.Remove(existingThemeDictionary);
            }

            // Add the new theme dictionary
            var themeDictionary = new ResourceDictionary { Source = themeUri };
            Current.Resources.MergedDictionaries.Add(themeDictionary);
        }

        private static Mutex _mutex; 
        
        [STAThread]
        public static void Main()
        {
            const string APP_NAME = "UnoraLaunchpadSingleInstance";
            _mutex = new Mutex(true, APP_NAME, out var isNewInstance);

            if (!isNewInstance)
            {
                MessageBox.Show("Unora Launchpad is already running. Check your existing taskbar icons.", "Duplicate Launchpad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Construct the App FIRST!
            var app = new App();

            // Theme selection code MUST come after App is created:
            var settingsPath = "LauncherSettings/settings.json";
            Settings launcherSettings = null;
            if (File.Exists(settingsPath))
                launcherSettings = FileService.LoadSettings(settingsPath);
            launcherSettings ??= new Settings();

            var themeName = launcherSettings.SelectedTheme;
            if (string.IsNullOrEmpty(themeName))
                themeName = "Dark";

            var themeFile = themeName switch
            {
                "Light"   => "LightTheme.xaml",
                "Teal"    => "TealTheme.xaml",
                "Violet"  => "VioletTheme.xaml",
                "Amber"   => "AmberTheme.xaml",
                "Emerald" => "EmeraldTheme.xaml",
                _         => "DarkTheme.xaml"
            };

            // THIS IS NOW SAFE, because App is constructed
            var themeUri = new Uri($"pack://application:,,,/Resources/{themeFile}", UriKind.Absolute);

            // Remove any existing theme resource
            var existingThemeDictionary = app.Resources.MergedDictionaries
                                             .FirstOrDefault(d => d.Source != null && d.Source.ToString().EndsWith("Theme.xaml"));
            if (existingThemeDictionary != null)
                app.Resources.MergedDictionaries.Remove(existingThemeDictionary);
            var themeDictionary = new ResourceDictionary { Source = themeUri };
            app.Resources.MergedDictionaries.Add(themeDictionary);

            // Now startup as usual
            app.InitializeComponent();
            var mainWindow = new MainWindow();
            app.Run(mainWindow);

            _mutex.ReleaseMutex();
        }

    }
}