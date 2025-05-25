using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace UnoraLaunchpad;

internal sealed partial class SettingsWindow : Window
{
    private readonly MainWindow _mainWindow;
    private readonly Settings _settings;

    public SettingsWindow(MainWindow mainWindow, Settings settings)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        _settings = settings;
        // LoadSettings(); // Call will be made by Window.Loaded event
    }

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSettings();
    }

    private void LoadSettings()
    {
        // _settings is already initialized by the constructor

        DawndCheckBox.IsChecked = _settings.UseDawndWindower;
        SkipIntroCheckBox.IsChecked = _settings.SkipIntro;
        LocalhostCheckBox.IsChecked = _settings.UseLocalhost;

        string currentTheme = "Dark"; // Default
        if (!string.IsNullOrEmpty(_settings.SelectedTheme))
        {
            currentTheme = _settings.SelectedTheme;
        }
        else // If no theme is set in settings, default to "Dark" and update the settings object
        {
            _settings.SelectedTheme = "Dark";
        }

        // Set ComboBox
        ThemeComboBox.SelectedItem = ThemeComboBox.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(cbi => cbi.Content.ToString() == currentTheme) ?? ThemeComboBox.Items[0];

        // Apply the loaded theme
        Uri themeUri;
        if (currentTheme == "Light")
        {
            themeUri = new Uri("pack://application:,,,/Resources/LightTheme.xaml", UriKind.Absolute);
        }
        else // Dark or any other case
        {
            themeUri = new Uri("pack://application:,,,/Resources/DarkTheme.xaml", UriKind.Absolute);
        }
        App.ChangeTheme(themeUri);
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            string themeName = selectedItem.Content.ToString();
            Uri themeUri;
            if (themeName == "Light")
            {
                themeUri = new Uri("pack://application:,,,/Resources/LightTheme.xaml", UriKind.Absolute);
            }
            else // Default to Dark
            {
                themeUri = new Uri("pack://application:,,,/Resources/DarkTheme.xaml", UriKind.Absolute);
            }
            App.ChangeTheme(themeUri);
        }
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        _settings.UseDawndWindower = DawndCheckBox.IsChecked ?? false;
        _settings.SkipIntro = SkipIntroCheckBox.IsChecked ?? false;
        _settings.UseLocalhost = LocalhostCheckBox.IsChecked ?? false;

        if (ThemeComboBox.SelectedItem is ComboBoxItem selectedThemeItem)
        {
            _settings.SelectedTheme = selectedThemeItem.Content.ToString();
        }
        else
        {
            _settings.SelectedTheme = "Dark"; // Fallback, though ComboBox should always have a selection
        }
        
        _mainWindow.SaveSettings(_settings); // Persist settings through MainWindow
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}