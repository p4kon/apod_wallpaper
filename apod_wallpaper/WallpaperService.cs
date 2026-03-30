namespace apod_wallpaper
{
    internal sealed class WallpaperService
    {
        public void ApplyPreservingHistory(string imagePath, WallpaperStyle style)
        {
            WallpaperNative.SilentSet(imagePath, style);
        }
    }
}
