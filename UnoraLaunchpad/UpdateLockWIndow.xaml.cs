using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace UnoraLaunchpad
{
    public partial class UpdateLockWindow : Window
    {
        public UpdateLockWindow()
        {
            InitializeComponent();
            Loaded += (_, __) => UpdateStatus();
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
                StatusText.Text = $"Close {procs.Length} instance(s) of Unora to proceed.";
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
    }
}