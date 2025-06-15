using System;
using System.IO;
using System.Diagnostics; // For Debug.WriteLine

namespace UnoraLaunchpad.Utils
{
    /// <summary>
    /// Provides static methods for logging messages and exceptions to a text file.
    /// Ensures that the log directory exists before attempting to write to the log file.
    /// Logging errors themselves are suppressed to prevent the application from crashing due to logging issues.
    /// </summary>
    public static class LoggingService
    {
        private static readonly string LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LauncherSettings");
        private static readonly string LogFilePath = Path.Combine(LogDirectory, "log.txt");
        private static readonly object LogLock = new object(); // Lock object for thread safety

        /// <summary>
        /// Ensures that the log directory exists. If not, it attempts to create it.
        /// Exceptions during directory creation are caught and logged to Debug output if possible.
        /// </summary>
        private static void EnsureLogDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }
            }
            catch (Exception ex)
            {
                // Suppress exceptions during log directory creation, but log to debug output.
                Debug.WriteLine($"[LoggingService] Failed to create log directory '{LogDirectory}': {ex.Message}");
            }
        }

        /// <summary>
        /// Logs an error message and details of an exception to the log file with a timestamp.
        /// </summary>
        /// <param name="message">The error message to log.</param>
        /// <param name="ex">The exception to log. Can be null.</param>
        public static void LogError(string message, Exception ex)
        {
            string fullMessage = message;
            if (ex != null)
            {
                fullMessage = $"{message} Exception: {ex}"; // ex.ToString() includes stack trace
            }
            LogMessage(fullMessage, "ERROR");
        }

        /// <summary>
        /// Logs an exception to the log file, including a timestamp.
        /// </summary>
        /// <param name="e">The exception to log.</param>
        public static void LogException(Exception e)
        {
            if (e == null) return;
            LogMessage($"[EXCEPTION]: {e}", "EXCEPTION"); // Pass "EXCEPTION" as a distinct type for LogMessage
        }

        /// <summary>
        /// Logs an informational message to the log file with a timestamp.
        /// </summary>
        /// <param name="message">The informational message to log.</param>
        public static void LogInfo(string message)
        {
            LogMessage(message, "INFO");
        }

        /// <summary>
        /// Logs a warning message to the log file with a timestamp.
        /// </summary>
        /// <param name="message">The warning message to log.</param>
        public static void LogWarning(string message)
        {
            LogMessage(message, "WARNING");
        }

        /// <summary>
        /// Logs an error message to the log file with a timestamp.
        /// </summary>
        /// <param name="message">The error message to log.</param>
        public static void LogError(string message)
        {
            LogMessage(message, "ERROR");
        }

        /// <summary>
        /// Private helper method to handle the actual file writing for all log types.
        /// Includes a timestamp and the log type prefix.
        /// </summary>
        /// <param name="message">The message content to log.</param>
        /// <param name="logLevel">The level of the log (e.g., INFO, WARNING, ERROR, EXCEPTION).</param>
        private static void LogMessage(string message, string logLevel)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            EnsureLogDirectoryExists();

            try
            {
                lock (LogLock) // Ensure thread-safe file access
                {
                    // Using a specific format for timestamp to ensure consistency
                    File.AppendAllText(LogFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}]: {message}\n");
                }
            }
            catch (Exception ex)
            {
                // Suppress logging errors to prevent app crashes, but log to debug output.
                Debug.WriteLine($"[LoggingService] Failed to write to log file '{LogFilePath}': {ex.Message}");
            }
        }
    }
}
