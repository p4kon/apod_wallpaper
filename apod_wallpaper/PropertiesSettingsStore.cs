namespace apod_wallpaper
{
    internal sealed class PropertiesSettingsStore : IApplicationSettingsStore
    {
        public ApplicationSettingsSnapshot Load()
        {
            return new ApplicationSettingsSnapshot
            {
                TrayDoubleClickAction = Properties.Settings.Default.TrayDoubleClickAction,
                WallpaperStyleIndex = Properties.Settings.Default.StyleComboBox,
                AutoRefreshEnabled = Properties.Settings.Default.AutoRefreshEnabled,
                StartWithWindows = Properties.Settings.Default.StartWithWindows,
                NasaApiKeyValidationState = Properties.Settings.Default.NasaApiKeyValidationState,
                ImagesDirectoryPath = Properties.Settings.Default.ImagesDirectoryPath,
                LastAutoRefreshRunDate = Properties.Settings.Default.LastAutoRefreshRunDate,
                LastAutoRefreshAppliedDate = Properties.Settings.Default.LastAutoRefreshAppliedDate,
            };
        }

        public void Save(ApplicationSettingsSnapshot settings)
        {
            Properties.Settings.Default.TrayDoubleClickAction = settings.TrayDoubleClickAction;
            Properties.Settings.Default.StyleComboBox = settings.WallpaperStyleIndex;
            Properties.Settings.Default.AutoRefreshEnabled = settings.AutoRefreshEnabled;
            Properties.Settings.Default.StartWithWindows = settings.StartWithWindows;
            Properties.Settings.Default.NasaApiKey = string.Empty;
            Properties.Settings.Default.NasaApiKeyValidationState = settings.NasaApiKeyValidationState;
            Properties.Settings.Default.ImagesDirectoryPath = settings.ImagesDirectoryPath;
            Properties.Settings.Default.LastAutoRefreshRunDate = settings.LastAutoRefreshRunDate;
            Properties.Settings.Default.LastAutoRefreshAppliedDate = settings.LastAutoRefreshAppliedDate;
            Properties.Settings.Default.Save();
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
    }
}
