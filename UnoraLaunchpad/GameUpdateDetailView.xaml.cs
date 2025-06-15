using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Microsoft.Xaml.Behaviors.Core;
using UnoraLaunchpad.Models; // Required for GameUpdate, Settings

namespace UnoraLaunchpad;

public sealed partial class GameUpdateDetailView
{
    public ICommand CloseCommand { get; private set; }

    private static readonly Dictionary<string, string> ThemeResourceMap = new()
    {
        { "Dark", "pack://application:,,,/Resources/DarkTheme.xaml" },
        { "Light", "pack://application:,,,/Resources/LightTheme.xaml" },
        { "Teal", "pack://application:,,,/Resources/TealTheme.xaml" },
        { "Violet", "pack://application:,,,/Resources/VioletTheme.xaml" },
        { "Amber", "pack://application:,,,/Resources/AmberTheme.xaml" },
        { "Emerald", "pack://application:,,,/Resources/EmeraldTheme.xaml" }
    };

    public GameUpdateDetailView(GameUpdate gameUpdate)
    {
        InitializeComponent();
        DataContext = gameUpdate;

        CloseCommand = new ActionCommand(Close);
        Loaded += GameUpdateDetailView_Loaded;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void GameUpdateDetailView_Loaded(object sender, RoutedEventArgs e)
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

        // Create a DoubleAnimation to change the opacity
        var animation = new DoubleAnimation
        {
            From = 0, // Start opacity
            To = 1, // End opacity
            Duration = new Duration(TimeSpan.FromSeconds(0.5)) // Duration of the animation
        };

        // Apply the animation to the window's Opacity property
        BeginAnimation(OpacityProperty, animation);
    }

    private static Uri GetThemeUri(string themeName)
    {
        if (!ThemeResourceMap.TryGetValue(themeName, out var uriString))
            uriString = ThemeResourceMap["Dark"]; // Default fallback
        return new Uri(uriString, UriKind.Absolute);
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}