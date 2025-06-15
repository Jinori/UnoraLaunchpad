using System;
using System.IO;
using System.Security.Cryptography;

namespace UnoraLaunchpad.Utils
{
    /// <summary>
    /// Provides utility methods for file hashing operations.
    /// Currently supports MD5 hash calculation.
    /// </summary>
    public static class FileHashHelper
    {
        /// <summary>
        /// Calculates an MD5 hash for a specified file.
        /// </summary>
        /// <param name="filePath">The absolute path to the file for which the hash is to be calculated.</param>
        /// <returns>A string representation of the MD5 hash (e.g., "09-8F-6B-CD-46-21-D3-73-CA-DE-4E-83-26-27-B4-F6").
        /// Returns null if the file does not exist or an error occurs during reading.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="filePath"/> is null or empty.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the specified <paramref name="filePath"/> does not exist.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory specified in <paramref name="filePath"/> does not exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown if the caller does not have the required permission to access the file.</exception>
        /// <exception cref="IOException">Thrown if an I/O error occurs while opening the file.</exception>
        public static string CalculateHash(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            // File.OpenRead will throw specific exceptions if file not found, etc.
            // These are appropriate to bubble up as they indicate issues the caller should be aware of.
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hashBytes = md5.ComputeHash(stream);
            return BitConverter.ToString(hashBytes); // Example: "09-8F-6B-CD-46-21-D3-73-CA-DE-4E-83-26-27-B4-F6"
        }
    }
}
