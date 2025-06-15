using System;
using System.Windows;
using UnoraLaunchpad.Models;    // For Settings, GameUpdate
using UnoraLaunchpad.Interfaces; // Required for INavigationService

namespace UnoraLaunchpad.Services
{
    /// <summary>
    /// Provides services for navigating between different windows and views within the application.
    /// This service abstracts the logic for instantiating and displaying various UI dialogs and windows.
    /// </summary>
    public class NavigationService : INavigationService
    {
        /// <summary>
        /// Displays the patch notes window as a dialog.
        /// </summary>
        /// <param name="owner">The window that will own the patch notes window.</param>
        public void ShowPatchNotes(Window owner)
        {
            var patchWindow = new PatchNotesWindow();
            patchWindow.Owner = owner;
            patchWindow.ShowDialog();
        }

        /// <summary>
        /// Displays the settings window.
        /// </summary>
        /// <param name="owner">The window that will own the settings window. This is expected to be the MainWindow.</param>
        /// <param name="currentSettings">The current settings to be displayed and modified in the settings window.</param>
        /// <param name="saveSettingsCallback">A callback action that the settings window will invoke to save the modified settings.
        /// This callback is typically implemented in the MainWindow.</param>
        /// <remarks>
        /// The current implementation of <see cref="SettingsWindow"/> has a constructor dependency on <see cref="MainWindow"/>.
        /// This method passes the <paramref name="owner"/> cast as <see cref="MainWindow"/> to satisfy this.
        /// Ideally, <see cref="SettingsWindow"/> should be refactored to remove this direct dependency.
        /// </remarks>
        public void ShowSettings(Window owner, Settings currentSettings, Action<Settings> saveSettingsCallback)
        {
            // Ensure owner is MainWindow as SettingsWindow constructor expects it.
            // The saveSettingsCallback is intended for a refactored SettingsWindow.
            // Currently, SettingsWindow calls MainWindow.SaveSettings directly.
            if (owner is MainWindow mainOwner)
            {
                var settingsWindow = new SettingsWindow(mainOwner, currentSettings);
                settingsWindow.Owner = owner;
                settingsWindow.Show();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Error: Owner window for Settings is not MainWindow. Actual type: {owner?.GetType().FullName}");
            }
        }

        /// <summary>
        /// Displays the game update detail view as a dialog.
        /// </summary>
        /// <param name="gameUpdate">The game update information to display.</param>
        /// <param name="owner">The window that will own the game update detail view.</param>
        public void ShowGameUpdateDetail(GameUpdate gameUpdate, Window owner)
        {
            var detailView = new GameUpdateDetailView(gameUpdate); // Assumes GameUpdateDetailView is defined
            detailView.Owner = owner;
            detailView.ShowDialog();
        }
    }
}
