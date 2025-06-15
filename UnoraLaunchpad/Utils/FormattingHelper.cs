using System;

namespace UnoraLaunchpad.Utils
{
    /// <summary>
    /// Provides utility methods for formatting data, such as file sizes and download speeds,
    /// into human-readable strings.
    /// </summary>
    public static class FormattingHelper
    {
        /// <summary>
        /// Formats a byte value into a human-readable string (e.g., KB, MB, GB).
        /// </summary>
        /// <param name="bytes">The number of bytes to format.</param>
        /// <returns>
        /// A string representing the formatted byte size.
        /// Returns "??" if the input is negative.
        /// Examples: "1.50 GB", "256.00 MB", "128.00 KB", "500 B".
        /// </returns>
        public static string FormatBytes(long bytes)
        {
            if (bytes < 0) return "??"; // Indicate unknown or invalid size for negative values

            const int scale = 1024;
            string[] orders = { "GB", "MB", "KB", "B" }; // Orders of magnitude
            long max = (long)Math.Pow(scale, orders.Length - 1);

            if (bytes > max) // Handles GB and larger, though GB is the largest defined unit here
                return string.Format("{0:F2} {1}", bytes / (double)max, orders[0]);

            if (bytes > Math.Pow(scale, 2)) // MB
                return string.Format("{0:F2} {1}", bytes / Math.Pow(scale, 2), orders[1]);

            if (bytes > scale) // KB
                return string.Format("{0:F2} {1}", bytes / (double)scale, orders[2]);

            return string.Format("{0} {1}", bytes, orders[3]); // Bytes
        }

        /// <summary>
        /// Formats a download/upload speed (bytes per second) into a human-readable string (e.g., KB/s, MB/s).
        /// </summary>
        /// <param name="bytesPerSec">The speed in bytes per second.</param>
        /// <returns>
        /// A string representing the formatted speed.
        /// Examples: "2.00 MB/s", "512.00 KB/s", "250.00 B/s".
        /// Returns "0.00 B/s" if input is negative, though speed should ideally be non-negative.
        /// </returns>
        public static string FormatSpeed(double bytesPerSec)
        {
            if (bytesPerSec < 0) bytesPerSec = 0; // Treat negative speed as 0 for display

            const int scale = 1024;
            string[] orders = { "MB/s", "KB/s", "B/s" }; // Orders of magnitude for speed

            if (bytesPerSec >= Math.Pow(scale, 2)) // MB/s
                return string.Format("{0:F2} {1}", bytesPerSec / Math.Pow(scale, 2), orders[0]);

            if (bytesPerSec >= scale) // KB/s
                return string.Format("{0:F2} {1}", bytesPerSec / (double)scale, orders[1]);

            return string.Format("{0:F2} {1}", bytesPerSec, orders[2]); // B/s
        }
    }
}
