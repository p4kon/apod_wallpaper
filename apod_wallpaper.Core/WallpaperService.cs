using System;

namespace apod_wallpaper
{
    internal sealed class WallpaperService : IWallpaperApplier
    {
        public void ApplyPreservingHistory(string imagePath, WallpaperStyle style)
        {
            if (!LocalImageValidator.IsUsableImageFile(imagePath))
                throw new InvalidOperationException("Wallpaper image file is missing, invalid, or not a supported image format: " + (imagePath ?? "<null>"));

            var effectiveImagePath = imagePath;
            var effectiveStyle = style;

            if (style == WallpaperStyle.Smart)
            {
                var composition = SmartWallpaperComposer.Prepare(imagePath);
                if (!LocalImageValidator.IsUsableImageFile(composition.ImagePath))
                    throw new InvalidOperationException("Prepared smart wallpaper image is invalid: " + (composition.ImagePath ?? "<null>"));

                effectiveImagePath = composition.ImagePath;
                effectiveStyle = composition.Style;
                AppLogger.Info("Applied smart wallpaper strategy=" + composition.Strategy + " source=" + imagePath + " target=" + effectiveImagePath + " style=" + effectiveStyle + ".");
            }
            else
            {
                AppLogger.Info("Applying wallpaper source=" + imagePath + " style=" + effectiveStyle + ".");
            }

            WallpaperNative.SilentSet(effectiveImagePath, effectiveStyle);
        }
    }
}
