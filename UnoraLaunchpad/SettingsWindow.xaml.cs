using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UnoraLaunchpad.Models; // Required for Settings

namespace UnoraLaunchpad;

internal sealed partial class SettingsWindow : Window
{
    private readonly MainWindow _mainWindow;
    private readonly Settings _settings;
    private static readonly Dictionary<string, string> ThemeResourceMap = new()
    {
        { "Dark",  "pack://application:,,,/Resources/DarkTheme.xaml" },
        { "Light", "pack://application:,,,/Resources/LightTheme.xaml" },
        { "Teal",  "pack://application:,,,/Resources/TealTheme.xaml" },
        { "Violet", "pack://application:,,,/Resources/VioletTheme.xaml" },
        { "Amber", "pack://application:,,,/Resources/AmberTheme.xaml" },
        { "Emerald", "pack://application:,,,/Resources/EmeraldTheme.xaml" }
    };


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
        DawndCheckBox.IsChecked = _settings.UseDawndWindower;
        SkipIntroCheckBox.IsChecked = _settings.SkipIntro;
        LocalhostCheckBox.IsChecked = _settings.UseLocalhost;

        var currentTheme = string.IsNullOrEmpty(_settings.SelectedTheme) ? "Dark" : _settings.SelectedTheme;
        _settings.SelectedTheme = currentTheme; // Ensure it's set for persistence

        // Set ComboBox to match the loaded theme
        ThemeComboBox.SelectedItem = ThemeComboBox.Items.Cast<ComboBoxItem>()
                                                  .FirstOrDefault(cbi => cbi.Content.ToString() == currentTheme) ?? ThemeComboBox.Items[0];

        var currentGame = string.IsNullOrEmpty(_settings.SelectedGame) ? "Unora" : _settings.SelectedGame;
        _settings.SelectedGame = currentGame;

        GameComboBox.SelectedItem = GameComboBox.Items.Cast<ComboBoxItem>()
                                                .FirstOrDefault(cbi => cbi.Content.ToString() == currentGame)
                                    ?? GameComboBox.Items[0];

        
        // Apply the loaded theme
        App.ChangeTheme(GetThemeUri(currentTheme));
    }


    private static Uri GetThemeUri(string themeName)
    {
        if (!ThemeResourceMap.TryGetValue(themeName, out var uriString))
            uriString = ThemeResourceMap["Dark"]; // Default fallback
        return new Uri(uriString, UriKind.Absolute);
    }

    
    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            var themeName = selectedItem.Content.ToString();
            App.ChangeTheme(GetThemeUri(themeName));
            // Optionally, update settings so it persists:
            _settings.SelectedTheme = themeName; 
        }
    }


    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        _settings.UseDawndWindower = DawndCheckBox.IsChecked ?? false;
        _settings.SkipIntro = SkipIntroCheckBox.IsChecked ?? false;
        _settings.UseLocalhost = LocalhostCheckBox.IsChecked ?? false;

        if (GameComboBox.SelectedItem is ComboBoxItem selectedGameItem)
            _settings.SelectedGame = selectedGameItem.Content.ToString();
        else
            _settings.SelectedGame = "Unora";

        
        if (ThemeComboBox.SelectedItem is ComboBoxItem selectedThemeItem)
        {
            _settings.SelectedTheme = selectedThemeItem.Content.ToString();
        }
        else
        {
            _settings.SelectedTheme = "Dark"; // Fallback, though ComboBox should always have a selection
        }
        

        _mainWindow.SaveSettings(_settings); // Persist settings through MainWindow
        _mainWindow.ReloadSettingsAndRefresh();
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}