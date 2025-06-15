using System.Windows;

namespace UnoraLaunchpad
{
    public partial class PasswordPromptDialog : Window
    {
        public string Password { get; private set; }

        public PasswordPromptDialog(string username)
        {
            InitializeComponent();
            Title = $"Password for {username}";
            PromptText.Text = $"Enter password for \"{username}\":"; // Corrected string interpolation
            PasswordInputBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Password = PasswordInputBox.Password;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
