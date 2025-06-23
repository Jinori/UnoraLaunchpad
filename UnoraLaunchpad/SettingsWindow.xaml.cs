using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
        // Add at the end of SettingsWindow_Loaded:
        AccountsListBox_SelectionChanged(null, null); // To set initial state of buttons and textboxes
        MacrosListBox_SelectionChanged(null, null); // To set initial state for macro buttons and textboxes
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

        // === Add these lines for account management ===
        if (_settings.SavedCharacters == null) // Defensive check, though FileService should handle this
        {
            _settings.SavedCharacters = new List<Character>();
        }
        CharactersListBox.ItemsSource = _settings.SavedCharacters;
        CharactersListBox.DisplayMemberPath = "Username";
        // === End of new lines for account management ===

        // === Add these lines for macro management ===
        if (_settings.Macros == null)
        {
            _settings.Macros = new Dictionary<string, string>();
        }
        MacrosListBox.ItemsSource = _settings.Macros.ToList(); // Bind to a list of KeyValuePair
        // MacrosListBox.DisplayMemberPath = "Key"; // Using DataTemplate instead
        // === End of new lines for macro management ===
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

    // New event handlers
    private void AccountsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CharactersListBox.SelectedItem is Character selectedAccount)
        {
            UsernameTextBox.Text = selectedAccount.Username;
            // PasswordTextBox.Password = selectedAccount.Password; // Password is no longer stored directly
            PasswordTextBox.Password = ""; // Clear password box or prompt for change
            EditAccountButton.IsEnabled = true;
            RemoveAccountButton.IsEnabled = true;
        }
        else
        {
            UsernameTextBox.Text = string.Empty;
            PasswordTextBox.Password = string.Empty;
            EditAccountButton.IsEnabled = false;
            RemoveAccountButton.IsEnabled = false;
        }
    }

    private void AddCharacterButton_Click(object sender, RoutedEventArgs e)
    {
        var username = UsernameTextBox.Text.Trim();
        var password = PasswordTextBox.Password;

        if (string.IsNullOrWhiteSpace(username))
        {
            MessageBox.Show("Username cannot be empty.", "Add Character", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Optional: Check for duplicate usernames
        if (_settings.SavedCharacters.Any(acc => acc.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("An character with this username already exists.", "Add Character", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var encryptedPassword = PasswordHelper.EncryptString(password);
        var newAccount = new Character
        {
            Username = username,
            EncryptedPassword = encryptedPassword,
            Password = null // Ensure plaintext password is not stored
        };
        _settings.SavedCharacters.Add(newAccount);

        RefreshCharactersListBox();
        UsernameTextBox.Text = string.Empty;
        PasswordTextBox.Password = string.Empty;
    }

    private void EditCharacterButton_Click(object sender, RoutedEventArgs e)
    {
        if (CharactersListBox.SelectedItem is Character selectedAccount)
        {
            var updatedUsername = UsernameTextBox.Text.Trim();
            var updatedPassword = PasswordTextBox.Password;

            if (string.IsNullOrWhiteSpace(updatedUsername))
            {
                MessageBox.Show("Username cannot be empty.", "Update Account", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Optional: Check if username is changed to one that already exists (excluding itself)
            if (!selectedAccount.Username.Equals(updatedUsername, StringComparison.OrdinalIgnoreCase) &&
                _settings.SavedCharacters.Any(acc => acc != selectedAccount && acc.Username.Equals(updatedUsername, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Another account with this username already exists.", "Update Account", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            selectedAccount.Username = updatedUsername;
            // Only update password hash and salt if a new password is provided
            if (!string.IsNullOrWhiteSpace(updatedPassword))
            {
                selectedAccount.EncryptedPassword = PasswordHelper.EncryptString(updatedPassword);
                selectedAccount.Password = null; // Ensure plaintext password is not stored
            }
            // If updatedPassword is blank, we assume the user does not want to change the password.
            // The existing PasswordHash and Salt remain.

            RefreshCharactersListBox();
            // Keep text boxes populated with edited info, user might want to edit further or deselect
            // UsernameTextBox.Text = string.Empty;
            // PasswordTextBox.Password = string.Empty;
            // AccountsListBox.SelectedItem = null; // Deselect
        }
        else
        {
            MessageBox.Show("Please select an account to update.", "Update Account", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void RemoveCharacterButton_Click(object sender, RoutedEventArgs e)
    {
        if (CharactersListBox.SelectedItem is Character selectedAccount)
        {
            var result = MessageBox.Show($"Are you sure you want to remove the character '{selectedAccount.Username}'?",
                                         "Remove Account", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                _settings.SavedCharacters.Remove(selectedAccount);
                RefreshCharactersListBox();
                UsernameTextBox.Text = string.Empty; // Clear after removal
                PasswordTextBox.Password = string.Empty;
            }
        }
        else
        {
            MessageBox.Show("Please select an account to remove.", "Remove Account", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // Helper to refresh ListBox
    private void RefreshCharactersListBox()
    {
        CharactersListBox.ItemsSource = null;
        CharactersListBox.ItemsSource = _settings.SavedCharacters;
        CharactersListBox.DisplayMemberPath = "Username";
    }

    // === New event handlers for macros ===
    private void MacrosListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MacrosListBox.SelectedItem is KeyValuePair<string, string> selectedMacro)
        {
            MacroTriggerKeyTextBox.Text = selectedMacro.Key;
            MacroActionSequenceTextBox.Text = selectedMacro.Value;
            EditMacroButton.IsEnabled = true;
            RemoveMacroButton.IsEnabled = true;
        }
        else
        {
            MacroTriggerKeyTextBox.Text = string.Empty;
            MacroActionSequenceTextBox.Text = string.Empty;
            EditMacroButton.IsEnabled = false;
            RemoveMacroButton.IsEnabled = false;
        }
    }

    private void AddMacroButton_Click(object sender, RoutedEventArgs e)
    {
        var triggerKey = MacroTriggerKeyTextBox.Text.Trim();
        var actionSequence = MacroActionSequenceTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(triggerKey))
        {
            MessageBox.Show("Trigger key cannot be empty.", "Add Macro", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(actionSequence))
        {
            MessageBox.Show("Action sequence cannot be empty.", "Add Macro", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_settings.Macros.ContainsKey(triggerKey))
        {
            MessageBox.Show("A macro with this trigger key already exists.", "Add Macro", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.Macros.Add(triggerKey, actionSequence);
        RefreshMacrosListBox();
        MacroTriggerKeyTextBox.Text = string.Empty;
        MacroActionSequenceTextBox.Text = string.Empty;
    }

    private void EditMacroButton_Click(object sender, RoutedEventArgs e)
    {
        if (MacrosListBox.SelectedItem is KeyValuePair<string, string> selectedMacro)
        {
            var originalKey = selectedMacro.Key;
            var updatedTriggerKey = MacroTriggerKeyTextBox.Text.Trim();
            var updatedActionSequence = MacroActionSequenceTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(updatedTriggerKey))
            {
                MessageBox.Show("Trigger key cannot be empty.", "Update Macro", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(updatedActionSequence))
            {
                MessageBox.Show("Action sequence cannot be empty.", "Update Macro", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // If the key is changed, remove the old entry and add a new one
            // Also, check for conflicts if the key is changed to an existing key
            if (originalKey != updatedTriggerKey)
            {
                if (_settings.Macros.ContainsKey(updatedTriggerKey))
                {
                    MessageBox.Show("Another macro with this trigger key already exists.", "Update Macro", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _settings.Macros.Remove(originalKey);
                _settings.Macros.Add(updatedTriggerKey, updatedActionSequence);
            }
            else
            {
                // Key is the same, just update the value
                _settings.Macros[updatedTriggerKey] = updatedActionSequence;
            }

            RefreshMacrosListBox();
        }
        else
        {
            MessageBox.Show("Please select a macro to update.", "Update Macro", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void RemoveMacroButton_Click(object sender, RoutedEventArgs e)
    {
        if (MacrosListBox.SelectedItem is KeyValuePair<string, string> selectedMacro)
        {
            var result = MessageBox.Show($"Are you sure you want to remove the macro for '{selectedMacro.Key}'?",
                                         "Remove Macro", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                _settings.Macros.Remove(selectedMacro.Key);
                RefreshMacrosListBox();
                MacroTriggerKeyTextBox.Text = string.Empty;
                MacroActionSequenceTextBox.Text = string.Empty;
            }
        }
        else
        {
            MessageBox.Show("Please select a macro to remove.", "Remove Macro", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void RefreshMacrosListBox()
    {
        MacrosListBox.ItemsSource = null;
        MacrosListBox.ItemsSource = _settings.Macros.ToList(); // Refresh with a new list of KeyValuePairs
        // Ensure selection behavior is reset or maintained as desired
        if (MacrosListBox.Items.Count > 0)
        {
            MacrosListBox.SelectedIndex = -1; // Or reselect the previously selected/edited item if complex logic is needed
        }
        MacrosListBox_SelectionChanged(null, null); // Update button states
    }
    // === End of new event handlers for macros ===
}