using System.Windows;
using System.Windows.Input;

namespace UnoraLaunchpad;

internal sealed partial class SettingsWindow
{
    public bool SkipIntro { get; set; }
    public bool UseDawndWindower { get; set; }
    public bool UseLocalhost { get; set; }

    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = Application.Current.MainWindow as MainWindow;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is MainWindow mainWindow)
        {
            var settings = new Settings
            {
                UseDawndWindower = mainWindow.UseDawndWindower,
                UseLocalhost = mainWindow.UseLocalhost,
                SkipIntro = mainWindow.SkipIntro
            };

            mainWindow.SaveSettings(settings);
            Close();
        }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}