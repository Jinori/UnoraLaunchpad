using System;
using System.Windows; // For Window
using UnoraLaunchpad.Models; // For Settings, GameUpdate

namespace UnoraLaunchpad.Interfaces
{
    public interface INavigationService
    {
        void ShowPatchNotes(Window owner);
        void ShowSettings(Window owner, Settings currentSettings, Action<Settings> saveSettingsCallback);
        void ShowGameUpdateDetail(GameUpdate gameUpdate, Window owner);
    }
}
