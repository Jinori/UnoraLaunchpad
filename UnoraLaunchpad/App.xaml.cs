using System;
using System.Linq; // Add this for LINQ queries
using System.Threading;
using System.Windows;

namespace UnoraLaunchpad
{
    public partial class App : Application
    {
        public static void ChangeTheme(Uri themeUri)
        {
            // Find and remove existing theme dictionary if any
            var existingThemeDictionary = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && 
                                     (d.Source.ToString().EndsWith("DarkTheme.xaml") || 
                                      d.Source.ToString().EndsWith("LightTheme.xaml")));

            if (existingThemeDictionary != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(existingThemeDictionary);
            }

            // Add the new theme dictionary
            ResourceDictionary themeDictionary = new ResourceDictionary { Source = themeUri };
            Application.Current.Resources.MergedDictionaries.Add(themeDictionary);
        }
        
        private static Mutex _mutex;

        [STAThread]
        public static void Main()
        {
            const string APP_NAME = "UnoraLaunchpadSingleInstance";
            System.Net.ServicePointManager.DefaultConnectionLimit = 5;

            _mutex = new Mutex(true, APP_NAME, out var isNewInstance);

            if (!isNewInstance)
            {
                MessageBox.Show("Unora Launchpad is already running. Check your existing taskbar icons.", "Duplicate Launchpad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var app = new App();
            app.InitializeComponent();
            app.Run();

            _mutex.ReleaseMutex();
        }
    }
}