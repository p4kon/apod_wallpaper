using System;

namespace apod_wallpaper
{
    public sealed class ApplicationInitialStateSnapshot
    {
        public ApplicationSettingsSnapshot Settings { get; set; }
        public ApplicationStoragePaths StoragePaths { get; set; }
        public ApiKeyValidationState ApiKeyValidationState { get; set; }
        public DateTime PreferredDisplayDate { get; set; }
        public WallpaperStyle SelectedWallpaperStyle { get; set; }
        public bool LocalImageIndexReady { get; set; }
    }
}
