using System;
using System.Security.Cryptography;
using System.Text; // For Encoding if needed, though not directly for Rfc2898DeriveBytes password string

namespace UnoraLaunchpad
{
    public static class PasswordHelper
    {
        private const int SaltSize = 16; // bytes
        private const int HashSize = 32; // bytes
        private const int Iterations = 10000;

        public static string GenerateSalt()
        {
            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            return Convert.ToBase64String(salt);
        }

        public static string HashPassword(string password, string saltString)
        {
            byte[] salt = Convert.FromBase64String(saltString);
            // Password string to bytes conversion: UTF-8 is standard.
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

            using (var pbkdf2 = new Rfc2898DeriveBytes(passwordBytes, salt, Iterations))
            {
                byte[] hash = pbkdf2.GetBytes(HashSize);
                return Convert.ToBase64String(hash);
            }
        }

        public static bool VerifyPassword(string password, string storedHashString, string saltString)
        {
            byte[] salt = Convert.FromBase64String(saltString);
            byte[] storedHash = Convert.FromBase64String(storedHashString);
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

            using (var pbkdf2 = new Rfc2898DeriveBytes(passwordBytes, salt, Iterations))
            {
                byte[] testHash = pbkdf2.GetBytes(HashSize);
                // Simple byte-by-byte comparison
                if (testHash.Length != storedHash.Length) return false;
                for (int i = 0; i < testHash.Length; i++)
                {
                    if (testHash[i] != storedHash[i]) return false;
                }
                return true;
            }
        }
    }
}
