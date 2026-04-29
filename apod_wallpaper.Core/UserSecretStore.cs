using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace apod_wallpaper
{
    internal static class UserSecretStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("apod_wallpaper:nasa-api-key");
        private static string _secretDirectoryOverride;

        public static string GetNasaApiKey()
        {
            var path = GetNasaApiKeyPath();
            if (!File.Exists(path))
                return null;

            try
            {
                var encrypted = File.ReadAllBytes(path);
                if (encrypted.Length == 0)
                    return null;

                var decrypted = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
                var value = Encoding.UTF8.GetString(decrypted);
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Unable to read the stored NASA API key. The stored secret will be ignored.", ex);
                return null;
            }
        }

        public static void SaveNasaApiKey(string apiKey)
        {
            var normalized = Normalize(apiKey);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                DeleteNasaApiKey();
                return;
            }

            Directory.CreateDirectory(GetSecretsDirectory());
            var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(normalized), Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(GetNasaApiKeyPath(), encrypted);
        }

        public static void DeleteNasaApiKey()
        {
            var path = GetNasaApiKeyPath();
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Unable to delete the stored NASA API key.", ex);
            }
        }

        internal static void SetSecretDirectoryOverride(string directoryPath)
        {
            _secretDirectoryOverride = Normalize(directoryPath);
        }

        internal static void ClearSecretDirectoryOverride()
        {
            _secretDirectoryOverride = null;
        }

        private static string GetNasaApiKeyPath()
        {
            return Path.Combine(GetSecretsDirectory(), "nasa-api-key.bin");
        }

        private static string GetSecretsDirectory()
        {
            return string.IsNullOrWhiteSpace(_secretDirectoryOverride)
                ? FileStorage.SecretsDirectory
                : _secretDirectoryOverride;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
