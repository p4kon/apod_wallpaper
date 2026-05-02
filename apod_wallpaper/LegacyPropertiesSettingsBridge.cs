using System;

namespace apod_wallpaper
{
    internal sealed class LegacyPropertiesSettingsBridge : ILegacySettingsMigrationSource
    {
        public ApplicationSettingsSnapshot LoadLegacySettings()
        {
            return new ApplicationSettingsSnapshot
            {
                TrayDoubleClickAction = Properties.Settings.Default.TrayDoubleClickAction,
                WallpaperStyleIndex = Properties.Settings.Default.StyleComboBox,
                AutoRefreshEnabled = Properties.Settings.Default.AutoRefreshEnabled,
                StartWithWindows = Properties.Settings.Default.StartWithWindows,
                MinimizeToTrayOnClose = true,
                NasaApiKeyValidationState = Normalize(Properties.Settings.Default.NasaApiKeyValidationState, ApiKeyValidationState.Unknown.ToString()),
                ImagesDirectoryPath = Normalize(Properties.Settings.Default.ImagesDirectoryPath, string.Empty),
                LastAutoRefreshRunDate = Normalize(Properties.Settings.Default.LastAutoRefreshRunDate, string.Empty),
                LastAutoRefreshAppliedDate = Normalize(Properties.Settings.Default.LastAutoRefreshAppliedDate, string.Empty),
            };
        }

        public string LoadLegacyApiKey()
        {
            return Properties.Settings.Default.NasaApiKey;
        }

        public void ClearLegacyApiKey()
        {
            if (string.IsNullOrWhiteSpace(Properties.Settings.Default.NasaApiKey))
                return;

            Properties.Settings.Default.NasaApiKey = string.Empty;
            Properties.Settings.Default.Save();
        }

        public void ClearLegacySettingsPreservingApiKey()
        {
            var legacyApiKey = Properties.Settings.Default.NasaApiKey;
            Properties.Settings.Default.Reset();
            Properties.Settings.Default.NasaApiKey = legacyApiKey;
            Properties.Settings.Default.Save();
        }

        private static string Normalize(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
