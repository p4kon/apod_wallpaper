using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;

namespace apod_wallpaper
{
    internal static class FavoriteThumbnailService
    {
        private const int MaxThumbnailDimension = 240;
        private const long JpegQuality = 84L;
        private const string ThumbnailDirectoryName = "favorite-thumbnails";

        public static string GetOrCreateThumbnailPath(DateTime date, string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                return imagePath;

            try
            {
                FileStorage.EnsureCacheDirectory();
                var thumbnailDirectory = Path.Combine(FileStorage.CacheDirectory, ThumbnailDirectoryName);
                Directory.CreateDirectory(thumbnailDirectory);

                var sourceInfo = new FileInfo(imagePath);
                var thumbnailPath = Path.Combine(
                    thumbnailDirectory,
                    date.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".jpg");

                if (File.Exists(thumbnailPath) &&
                    File.GetLastWriteTimeUtc(thumbnailPath) >= sourceInfo.LastWriteTimeUtc)
                {
                    return thumbnailPath;
                }

                CreateThumbnail(imagePath, thumbnailPath);
                return thumbnailPath;
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Unable to create favorite thumbnail for " + imagePath + ".", ex);
                return imagePath;
            }
        }

        private static void CreateThumbnail(string imagePath, string thumbnailPath)
        {
            using (var source = Image.FromFile(imagePath))
            {
                var size = CalculateThumbnailSize(source.Width, source.Height);
                using (var thumbnail = new Bitmap(size.Width, size.Height))
                {
                    thumbnail.SetResolution(96, 96);
                    using (var graphics = Graphics.FromImage(thumbnail))
                    {
                        graphics.Clear(Color.Black);
                        graphics.CompositingQuality = CompositingQuality.HighQuality;
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.SmoothingMode = SmoothingMode.HighQuality;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        graphics.DrawImage(source, 0, 0, size.Width, size.Height);
                    }

                    SaveJpeg(thumbnail, thumbnailPath);
                }
            }
        }

        private static Size CalculateThumbnailSize(int sourceWidth, int sourceHeight)
        {
            if (sourceWidth <= 0 || sourceHeight <= 0)
                return new Size(MaxThumbnailDimension, MaxThumbnailDimension);

            var scale = Math.Min(
                MaxThumbnailDimension / (double)sourceWidth,
                MaxThumbnailDimension / (double)sourceHeight);
            scale = Math.Min(1d, scale);

            return new Size(
                Math.Max(1, (int)Math.Round(sourceWidth * scale)),
                Math.Max(1, (int)Math.Round(sourceHeight * scale)));
        }

        private static void SaveJpeg(Bitmap thumbnail, string thumbnailPath)
        {
            var encoder = ImageCodecInfo.GetImageEncoders()
                .FirstOrDefault(codec => string.Equals(codec.MimeType, "image/jpeg", StringComparison.OrdinalIgnoreCase));

            if (encoder == null)
            {
                thumbnail.Save(thumbnailPath, ImageFormat.Jpeg);
                return;
            }

            using (var parameters = new EncoderParameters(1))
            {
                parameters.Param[0] = new EncoderParameter(Encoder.Quality, JpegQuality);
                thumbnail.Save(thumbnailPath, encoder, parameters);
            }
        }
    }
}
