// ==============================================================================
// CredentialStorageService.cs - HomestayDesktopApp (.NET 8 WPF)
// ==============================================================================
// Provides local credential encryption using Windows Data Protection API (DPAPI).
// Credentials are encrypted with the Windows user account key at rest.
// Path: %LOCALAPPDATA%\HomestayDesktopApp\credentials.dat
// ==============================================================================

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HomestayDesktopApp.Services
{
    public class StoredCredentials
    {
        public string UserId { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public static class CredentialStorageService
    {
        // Path to store encrypted binary credential blob
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HomestayDesktopApp"
        );

        private static readonly string CredentialFilePath = Path.Combine(AppDataFolder, "credentials.dat");

        // Optional entropy salt for DPAPI additional layer
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("HomestayFormCKochi@2026");

        /// <summary>
        /// Saves Form-C portal login credentials using Windows DPAPI encryption.
        /// </summary>
        /// <param name="userId">Portal User ID.</param>
        /// <param name="password">Portal Password.</param>
        public static void SaveCredentials(string userId, string password)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Both User ID and Password must be provided.");
            }

            // Ensure destination directory exists
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }

            // Serialize credentials to JSON byte buffer
            var model = new StoredCredentials { UserId = userId.Trim(), Password = password };
            byte[] plainBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(model));

            // Encrypt using Windows DPAPI scoped to current logged-in user
            byte[] encryptedBytes = ProtectedData.Protect(
                plainBytes,
                Entropy,
                DataProtectionScope.CurrentUser
            );

            // Write encrypted blob to disk
            File.WriteAllBytes(CredentialFilePath, encryptedBytes);
        }

        /// <summary>
        /// Retrieves and decrypts the stored Form-C portal credentials.
        /// </summary>
        /// <returns>StoredCredentials model if present, otherwise null.</returns>
        public static StoredCredentials? LoadCredentials()
        {
            if (!File.Exists(CredentialFilePath))
            {
                return null;
            }

            try
            {
                byte[] encryptedBytes = File.ReadAllBytes(CredentialFilePath);
                byte[] plainBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    Entropy,
                    DataProtectionScope.CurrentUser
                );

                string json = Encoding.UTF8.GetString(plainBytes);
                return JsonSerializer.Deserialize<StoredCredentials>(json);
            }
            catch
            {
                // Return null if decryption fails (e.g., moved to another user account)
                return null;
            }
        }

        /// <summary>
        /// Checks whether saved credentials exist in local encrypted storage.
        /// </summary>
        public static bool HasCredentials() => File.Exists(CredentialFilePath);

        /// <summary>
        /// Deletes the local encrypted credential file.
        /// </summary>
        public static void ClearCredentials()
        {
            if (File.Exists(CredentialFilePath))
            {
                File.Delete(CredentialFilePath);
            }
        }
    }
}