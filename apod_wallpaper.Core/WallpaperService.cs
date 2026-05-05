using System;
using System.IO;

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

        public string ReapplyCurrentWallpaperStyle(WallpaperStyle style)
        {
            var currentWallpaperPath = ResolveCurrentWallpaperSourcePath();
            if (!LocalImageValidator.IsUsableImageFile(currentWallpaperPath))
                throw new InvalidOperationException("Current wallpaper image could not be resolved for style reapply: " + (currentWallpaperPath ?? "<null>"));

            ApplyPreservingHistory(currentWallpaperPath, style);
            return currentWallpaperPath;
        }

        public string ResolveCurrentWallpaperSourcePath()
        {
            var currentWallpaperPath = WallpaperNative.GetCurrentWallpaperPath();
            var originalSourcePath = TryResolveSmartWallpaperSourcePath(currentWallpaperPath);
            return LocalImageValidator.IsUsableImageFile(originalSourcePath)
                ? originalSourcePath
                : currentWallpaperPath;
        }

        public string ReapplyWallpaperStyle(string sourceImagePath, WallpaperStyle style)
        {
            if (LocalImageValidator.IsUsableImageFile(sourceImagePath))
            {
                ApplyPreservingHistory(sourceImagePath, style);
                return sourceImagePath;
            }

            return ReapplyCurrentWallpaperStyle(style);
        }

        private static string TryResolveSmartWallpaperSourcePath(string currentWallpaperPath)
        {
            if (string.IsNullOrWhiteSpace(currentWallpaperPath))
                return null;

            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(currentWallpaperPath);
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension) ||
                !fileNameWithoutExtension.EndsWith(".smart", StringComparison.OrdinalIgnoreCase))
                return null;

            var withoutSmartSuffix = fileNameWithoutExtension.Substring(0, fileNameWithoutExtension.Length - ".smart".Length);
            var resolutionSeparator = withoutSmartSuffix.LastIndexOf('.');
            if (resolutionSeparator <= 0)
                return null;

            var withoutResolution = withoutSmartSuffix.Substring(0, resolutionSeparator);
            var strategySeparator = withoutResolution.LastIndexOf('.');
            if (strategySeparator <= 0)
                return null;

            var strategy = withoutResolution.Substring(strategySeparator + 1);
            if (!string.Equals(strategy, "single", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(strategy, "collage", StringComparison.OrdinalIgnoreCase))
                return null;

            var originalBaseName = withoutResolution.Substring(0, strategySeparator);
            return FileStorage.TryFindExistingImagePath(originalBaseName);
        }
    }
}
