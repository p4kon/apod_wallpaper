namespace apod_wallpaper
{
    internal sealed class WallpaperService
    {
        public void ApplyPreservingHistory(string imagePath, WallpaperStyle style)
        {
            var effectiveImagePath = imagePath;
            var effectiveStyle = style;

            if (style == WallpaperStyle.Smart)
            {
                var composition = SmartWallpaperComposer.Prepare(imagePath);
                effectiveImagePath = composition.ImagePath;
                effectiveStyle = composition.Style;
                AppLogger.Info("Applied smart wallpaper strategy=" + composition.Strategy + " source=" + imagePath + " target=" + effectiveImagePath + " style=" + effectiveStyle + ".");
            }

            WallpaperNative.SilentSet(effectiveImagePath, effectiveStyle);
        }
    }
}
