using System;

namespace apod_wallpaper
{
    internal sealed class ApplicationSettingsSnapshot
    {
        public bool TrayDoubleClickAction { get; set; }
        public int WallpaperStyleIndex { get; set; }
        public DateTime RefreshTime { get; set; }
        public bool AutoRefreshEnabled { get; set; }
        public bool StartWithWindows { get; set; }
        public string NasaApiKey { get; set; }
        public string ImagesDirectoryPath { get; set; }

        public ApplicationSettingsSnapshot Clone()
        {
            return new ApplicationSettingsSnapshot
            {
                TrayDoubleClickAction = TrayDoubleClickAction,
                WallpaperStyleIndex = WallpaperStyleIndex,
                RefreshTime = RefreshTime,
                AutoRefreshEnabled = AutoRefreshEnabled,
                StartWithWindows = StartWithWindows,
                NasaApiKey = NasaApiKey,
                ImagesDirectoryPath = ImagesDirectoryPath,
            };
        }
    }
}
