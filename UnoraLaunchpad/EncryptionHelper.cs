using System;
using System.Security.Cryptography;
using System.Text; // For Encoding

namespace UnoraLaunchpad
{
    public static class EncryptionHelper // Formerly PasswordHelper
    {
        public static string EncryptString(string plainText, DataProtectionScope scope = DataProtectionScope.CurrentUser)
        {
            if (string.IsNullOrEmpty(plainText))
                return null;

            byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = ProtectedData.Protect(plainTextBytes, null, scope);
            return Convert.ToBase64String(encryptedBytes);
        }

        public static string DecryptString(string encryptedData, DataProtectionScope scope = DataProtectionScope.CurrentUser)
        {
            if (string.IsNullOrEmpty(encryptedData))
                return null;

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedData);
                byte[] plainTextBytes = ProtectedData.Unprotect(encryptedBytes, null, scope);
                return Encoding.UTF8.GetString(plainTextBytes);
            }
            catch (CryptographicException)
            {
                // Handle cases where decryption fails (e.g., wrong user, corrupted data)
                // Log this error appropriately in a real application
                System.Diagnostics.Debug.WriteLine($"DPAPI decryption failed for data. Scope: {scope}");
                return null;
            }
            catch (FormatException)
            {
                // Handle cases where Base64 string is invalid
                System.Diagnostics.Debug.WriteLine("DPAPI decryption failed due to invalid Base64 string.");
                return null;
            }
        }
    }
}
