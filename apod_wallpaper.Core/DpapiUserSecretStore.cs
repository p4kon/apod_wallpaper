using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace apod_wallpaper
{
    public sealed class DpapiUserSecretStore : IUserSecretStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("apod_wallpaper:nasa-api-key");
        private readonly string _secretDirectoryPathOverride;

        public DpapiUserSecretStore()
            : this(null)
        {
        }

        public DpapiUserSecretStore(string secretDirectoryPathOverride)
        {
            _secretDirectoryPathOverride = Normalize(secretDirectoryPathOverride);
        }

        public string GetNasaApiKey()
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

        public void SaveNasaApiKey(string apiKey)
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

        public void DeleteNasaApiKey()
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

        private string GetNasaApiKeyPath()
        {
            return Path.Combine(GetSecretsDirectory(), "nasa-api-key.bin");
        }

        private string GetSecretsDirectory()
        {
            return string.IsNullOrWhiteSpace(_secretDirectoryPathOverride)
                ? FileStorage.SecretsDirectory
                : _secretDirectoryPathOverride;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
