using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace UnoraLaunchpad
{
    internal partial class MacroWindow : Window
    {
        private readonly Settings _settings;
        private readonly MainWindow _mainWindow; // To call ApplyMacroSettingsAndSave

        // Use a local list for editing, then apply to _settings.Macros on save
        private List<KeyValuePair<string, string>> _localMacros;

        public MacroWindow(MainWindow mainWindow, Settings settings)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            _settings = settings;

            // Deep copy for local editing to avoid modifying original _settings.Macros directly until save
            _localMacros = _settings.Macros?.Select(kvp => new KeyValuePair<string, string>(kvp.Key, kvp.Value)).ToList()
                           ?? new List<KeyValuePair<string, string>>();
        }

        private void MacroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            EnableMacrosCheckBox.IsChecked = _settings.IsMacroSystemEnabled;
            RefreshMacrosListBox();
            UpdateButtonsState();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Offer to save if changes were made? For now, just close.
            // Or, consider if "Save & Close" is the only way to persist.
            // If user clicks 'X', changes are lost unless explicitly saved.
            this.Close();
        }

        private void SaveAndCloseBtn_Click(object sender, RoutedEventArgs e)
        {
            // Update the main settings object from local changes
            _settings.IsMacroSystemEnabled = EnableMacrosCheckBox.IsChecked ?? false;

            // Convert the local list back to dictionary for saving
            _settings.Macros.Clear();
            foreach (var kvp in _localMacros)
            {
                _settings.Macros[kvp.Key] = kvp.Value;
            }

            _mainWindow.ApplyMacroSettingsAndSave(); // This will save all settings and re-register hotkeys
            this.Close();
        }

        private void MacrosListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MacrosListBox.SelectedItem is KeyValuePair<string, string> selectedMacro)
            {
                MacroTriggerKeyTextBox.Text = selectedMacro.Key;
                MacroActionSequenceTextBox.Text = selectedMacro.Value;
            }
            else
            {
                MacroTriggerKeyTextBox.Text = string.Empty;
                MacroActionSequenceTextBox.Text = string.Empty;
            }
            UpdateButtonsState();
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
            if (_localMacros.Any(m => m.Key.Equals(triggerKey, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("A macro with this trigger key already exists.", "Add Macro", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _localMacros.Add(new KeyValuePair<string, string>(triggerKey, actionSequence));
            RefreshMacrosListBox();
            MacroTriggerKeyTextBox.Text = string.Empty;
            MacroActionSequenceTextBox.Text = string.Empty;
            MacrosListBox.SelectedItem = _localMacros.LastOrDefault(); // Select the newly added item
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

                // Check for new key conflict if key was changed
                if (!originalKey.Equals(updatedTriggerKey, StringComparison.OrdinalIgnoreCase) &&
                    _localMacros.Any(m => m.Key.Equals(updatedTriggerKey, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("Another macro with this trigger key already exists.", "Update Macro", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Find and update in the local list
                var itemIndex = _localMacros.FindIndex(m => m.Key.Equals(originalKey, StringComparison.OrdinalIgnoreCase));
                if (itemIndex != -1)
                {
                    _localMacros[itemIndex] = new KeyValuePair<string, string>(updatedTriggerKey, updatedActionSequence);
                    RefreshMacrosListBox();
                    MacrosListBox.SelectedItem = _localMacros[itemIndex]; // Reselect the updated item
                }
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
                    _localMacros.RemoveAll(m => m.Key.Equals(selectedMacro.Key, StringComparison.OrdinalIgnoreCase));
                    RefreshMacrosListBox();
                }
            }
        }

        private void RefreshMacrosListBox()
        {
            var previouslySelected = MacrosListBox.SelectedItem;
            MacrosListBox.ItemsSource = null;
            // Sort macros by key for consistent display, could be optional
            MacrosListBox.ItemsSource = _localMacros.OrderBy(m => m.Key).ToList();

            if (previouslySelected != null && _localMacros.Any(m => m.Key.Equals(((KeyValuePair<string,string>)previouslySelected).Key)))
            {
                 MacrosListBox.SelectedItem = _localMacros.First(m => m.Key.Equals(((KeyValuePair<string,string>)previouslySelected).Key));
            }
            else if (MacrosListBox.Items.Count > 0)
            {
                MacrosListBox.SelectedIndex = 0;
            }
            UpdateButtonsState();
        }

        private void UpdateButtonsState()
        {
            bool isItemSelected = MacrosListBox.SelectedItem != null;
            EditMacroButton.IsEnabled = isItemSelected;
            RemoveMacroButton.IsEnabled = isItemSelected;
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            string helpTitle = "Macro Syntax Help";
            string helpMessage = @"
Macro sequences are strings of characters and commands.

1. Literal Characters:
   - Any character outside of {} braces is typed directly.
   - Example: `Hello123` will type H, e, l, l, o, 1, 2, 3.
   - Case is respected: `aBc` types 'a', then SHIFT+'B', then 'c'.
   - Spaces outside of {} are ignored and can be used to format your macro string. `a b c` is same as `abc`.

2. Commands in Braces {}:
   - Commands allow for special actions like waiting or pressing specific keys.

   - {Wait Nms}: Pauses execution for N milliseconds.
     Example: `abc{Wait 500ms}def` (types abc, waits 0.5s, types def)

   - {KeyPress VK_CODE}: Simulates a full key press (down and up).
     VK_CODE is a Virtual Key Code name (e.g., RETURN, LCONTROL, SHIFT, F1, VK_A, VK_1).
     Example: `Name{KeyPress RETURN}` (types Name, then presses Enter)
     Example: `Chat{KeyPress VK_T}/say Hello{KeyPress RETURN}`

   - {KeyDown VK_CODE}: Simulates pressing a key down and holding it.
     Example: `Clip{KeyDown LCONTROL}{KeyPress VK_C}{KeyUp LCONTROL}` (Ctrl+C for copy)
     (The LCONTROL key is held down while C is pressed, then LCONTROL is released)

   - {KeyUp VK_CODE}: Simulates releasing a key.
     Example: `{KeyDown LSHIFT}abc{KeyUp LSHIFT}` (types ABC)

3. Combining:
   You can mix literal characters and commands.
   Example: `USE{KeyDown LSHIFT}SKILL1{KeyUp LSHIFT}{KeyPress RETURN}{Wait 500ms}Ready!`
   This types 'USE', holds SHIFT to type 'SKILL1', releases SHIFT, presses RETURN, waits 0.5s, then types 'Ready!'.

Common Virtual Key Codes (VK_CODE):
  - Letters: VK_A, VK_B, ..., VK_Z
  - Numbers: VK_0, VK_1, ..., VK_9 (above letters)
  - Numpad: NUMPAD0, NUMPAD1, ...
  - Function Keys: F1, F2, ..., F12
  - Modifiers: LSHIFT (Left Shift), RSHIFT (Right Shift), LCONTROL (Left Ctrl), RCONTROL (Right Ctrl), LMENU (Left Alt), RMENU (Right Alt)
  - Navigation: HOME, END, LEFT, RIGHT, UP, DOWN, PRIOR (PageUp), NEXT (PageDown)
  - Action: RETURN (Enter), TAB, ESCAPE, SPACE, BACK (Backspace), DELETE
  (Search 'VirtualKeyCode C#' for a more complete list from InputSimulatorStandard)
";
            MessageBox.Show(this, helpMessage, helpTitle, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
