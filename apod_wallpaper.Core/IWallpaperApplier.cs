namespace apod_wallpaper
{
    internal interface IWallpaperApplier
    {
        void ApplyPreservingHistory(string imagePath, WallpaperStyle style);
        string ReapplyCurrentWallpaperStyle(WallpaperStyle style);
        string ResolveCurrentWallpaperSourcePath();
    }
}
