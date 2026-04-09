using System.Collections.Generic;
using System.Drawing.Imaging;

namespace apod_wallpaper
{
    internal static class ImageFormatCatalog
    {
        private static readonly HashSet<string> SupportedImageExtensions = new HashSet<string>
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".bmp",
            ".gif",
            ".webp",
            ".tif",
            ".tiff",
        };

        public static bool IsSupportedImageExtension(string extension)
        {
            return !string.IsNullOrWhiteSpace(extension) && SupportedImageExtensions.Contains(extension.ToLowerInvariant());
        }

        public static string NormalizeImageExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return ".jpg";

            var normalized = extension.StartsWith(".")
                ? extension.Trim().ToLowerInvariant()
                : "." + extension.Trim().ToLowerInvariant();

            return IsSupportedImageExtension(normalized)
                ? normalized
                : ".png";
        }

        public static ImageFormat ResolveImageFormat(string extension)
        {
            switch (NormalizeImageExtension(extension))
            {
                case ".jpg":
                case ".jpeg":
                    return ImageFormat.Jpeg;
                case ".png":
                    return ImageFormat.Png;
                case ".bmp":
                    return ImageFormat.Bmp;
                case ".gif":
                    return ImageFormat.Gif;
                case ".tif":
                case ".tiff":
                    return ImageFormat.Tiff;
                default:
                    return ImageFormat.Png;
            }
        }
    }
}
