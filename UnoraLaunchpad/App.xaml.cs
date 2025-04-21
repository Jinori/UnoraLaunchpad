using System;
using System.Threading;
using System.Windows;

namespace UnoraLaunchpad
{
    public partial class App : Application
    {
        private static Mutex _mutex;

        [STAThread]
        public static void Main()
        {
            const string appName = "UnoraLaunchpadSingleInstance";

            _mutex = new Mutex(true, appName, out bool isNewInstance);

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