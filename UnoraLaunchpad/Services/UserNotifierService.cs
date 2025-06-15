using System.Windows;
using UnoraLaunchpad.Interfaces; // Required for IUserNotifierService

namespace UnoraLaunchpad.Services
{
    /// <summary>
    /// Provides a centralized way to display notifications and confirmation dialogs to the user.
    /// This service wraps standard <see cref="MessageBox"/> calls to ensure consistency
    /// and simplify UI interactions from other services or view models.
    /// </summary>
    public class UserNotifierService : IUserNotifierService
    {
        /// <summary>
        /// Displays an informational message to the user.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="title">The title of the message box window.</param>
        public void ShowMessage(string message, string title)
        {
            ShowMessageBoxWithOkButton(message, title, MessageBoxImage.Information);
        }

        /// <summary>
        /// Displays a warning message to the user.
        /// </summary>
        /// <param name="message">The warning message to display.</param>
        /// <param name="title">The title of the message box window.</param>
        public void ShowWarning(string message, string title)
        {
            ShowMessageBoxWithOkButton(message, title, MessageBoxImage.Warning);
        }

        /// <summary>
        /// Displays an error message to the user.
        /// </summary>
        /// <param name="message">The error message to display.</param>
        /// <param name="title">The title of the message box window.</param>
        public void ShowError(string message, string title)
        {
            ShowMessageBoxWithOkButton(message, title, MessageBoxImage.Error);
        }

        /// <summary>
        /// Private helper method to display a MessageBox with a standard OK button and a specified image.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="title">The title for the message box window.</param>
        /// <param name="image">The <see cref="MessageBoxImage"/> to display in the message box.</param>
        private void ShowMessageBoxWithOkButton(string message, string title, MessageBoxImage image)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, image);
        }

        /// <summary>
        /// Displays a confirmation dialog with "Yes" and "No" buttons.
        /// </summary>
        /// <param name="message">The question or message to display for confirmation.</param>
        /// <param name="title">The title of the message box window.</param>
        /// <returns><c>true</c> if the user clicks "Yes"; otherwise, <c>false</c>.</returns>
        public bool Confirm(string message, string title)
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }
    }
}
