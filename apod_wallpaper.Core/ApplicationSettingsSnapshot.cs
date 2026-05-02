namespace apod_wallpaper
{
    public sealed class ApplicationSettingsSnapshot
    {
        public bool TrayDoubleClickAction { get; set; }
        public int WallpaperStyleIndex { get; set; }
        public bool AutoRefreshEnabled { get; set; }
        public bool StartWithWindows { get; set; }
        public bool MinimizeToTrayOnClose { get; set; }
        public string NasaApiKey { get; set; }
        public string NasaApiKeyValidationState { get; set; }
        public string ImagesDirectoryPath { get; set; }
        public string LastAutoRefreshRunDate { get; set; }
        public string LastAutoRefreshAppliedDate { get; set; }

        public ApplicationSettingsSnapshot Clone()
        {
            return new ApplicationSettingsSnapshot
            {
                TrayDoubleClickAction = TrayDoubleClickAction,
                WallpaperStyleIndex = WallpaperStyleIndex,
                AutoRefreshEnabled = AutoRefreshEnabled,
                StartWithWindows = StartWithWindows,
                MinimizeToTrayOnClose = MinimizeToTrayOnClose,
                NasaApiKey = NasaApiKey,
                NasaApiKeyValidationState = NasaApiKeyValidationState,
                ImagesDirectoryPath = ImagesDirectoryPath,
                LastAutoRefreshRunDate = LastAutoRefreshRunDate,
                LastAutoRefreshAppliedDate = LastAutoRefreshAppliedDate,
            };
        }
    }
}
