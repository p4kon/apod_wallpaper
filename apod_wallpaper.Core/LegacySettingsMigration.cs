using System;

namespace apod_wallpaper
{
    public static class LegacySettingsMigration
    {
        private const string DemoApiKey = "DEMO_KEY";

        public static void MigrateIfNeeded(IApplicationSettingsStore settingsStore, IUserSecretStore secretStore, ILegacySettingsMigrationSource legacySource)
        {
            if (settingsStore == null)
                throw new ArgumentNullException(nameof(settingsStore));
            if (secretStore == null)
                throw new ArgumentNullException(nameof(secretStore));
            if (legacySource == null)
                throw new ArgumentNullException(nameof(legacySource));

            MigrateNonSecretSettingsIfNeeded(settingsStore, legacySource);
            MigrateApiKeyIfNeeded(secretStore, legacySource);
        }

        private static void MigrateNonSecretSettingsIfNeeded(IApplicationSettingsStore settingsStore, ILegacySettingsMigrationSource legacySource)
        {
            if (settingsStore.Exists())
                return;

            settingsStore.Save(legacySource.LoadLegacySettings() ?? new ApplicationSettingsSnapshot());
            legacySource.ClearLegacySettingsPreservingApiKey();
        }

        private static void MigrateApiKeyIfNeeded(IUserSecretStore secretStore, ILegacySettingsMigrationSource legacySource)
        {
            var storedApiKey = Normalize(secretStore.GetNasaApiKey());
            if (!string.IsNullOrWhiteSpace(storedApiKey))
            {
                legacySource.ClearLegacyApiKey();
                return;
            }

            var legacyApiKey = Normalize(legacySource.LoadLegacyApiKey());
            if (!string.IsNullOrWhiteSpace(legacyApiKey) &&
                !string.Equals(legacyApiKey, DemoApiKey, StringComparison.OrdinalIgnoreCase))
            {
                secretStore.SaveNasaApiKey(legacyApiKey);
            }

            legacySource.ClearLegacyApiKey();
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
