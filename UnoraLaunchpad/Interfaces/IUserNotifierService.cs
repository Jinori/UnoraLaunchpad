namespace UnoraLaunchpad.Interfaces
{
    public interface IUserNotifierService
    {
        void ShowMessage(string message, string title);
        void ShowWarning(string message, string title);
        void ShowError(string message, string title);
        bool Confirm(string message, string title);
    }
}
