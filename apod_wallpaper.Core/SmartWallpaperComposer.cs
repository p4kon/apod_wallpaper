using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace apod_wallpaper
{
    internal sealed class SmartWallpaperComposition
    {
        public string ImagePath { get; set; }
        public WallpaperStyle Style { get; set; }
        public string Strategy { get; set; }
    }

    internal static class SmartWallpaperComposer
    {
        private const double LandscapeTolerance = 0.18;
        private const double PortraitThreshold = 0.82;

        public static SmartWallpaperComposition Prepare(string originalImagePath)
        {
            if (string.IsNullOrWhiteSpace(originalImagePath) || !File.Exists(originalImagePath))
                throw new FileNotFoundException("Wallpaper image was not found.", originalImagePath);

            var screenBounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            using (var image = Image.FromFile(originalImagePath))
            {
                var imageAspect = image.Width / (double)image.Height;
                var screenAspect = screenBounds.Width / (double)screenBounds.Height;

                if (Math.Abs(imageAspect - screenAspect) <= LandscapeTolerance)
                {
                    return new SmartWallpaperComposition
                    {
                        ImagePath = originalImagePath,
                        Style = WallpaperStyle.Fill,
                        Strategy = "fill_near_screen_ratio",
                    };
                }

                if (imageAspect < PortraitThreshold)
                {
                    var composedPath = ComposePortraitWallpaper(originalImagePath, image, screenBounds);
                    return new SmartWallpaperComposition
                    {
                        ImagePath = composedPath,
                        Style = WallpaperStyle.Fill,
                        Strategy = "portrait_collage",
                    };
                }

                return new SmartWallpaperComposition
                {
                    ImagePath = originalImagePath,
                    Style = WallpaperStyle.Fit,
                    Strategy = "fit_preserve_image",
                };
            }
        }

        private static string ComposePortraitWallpaper(string originalImagePath, Image sourceImage, Rectangle screenBounds)
        {
            Directory.CreateDirectory(FileStorage.CacheDirectory);
            var targetPath = Path.Combine(
                FileStorage.CacheDirectory,
                Path.GetFileNameWithoutExtension(originalImagePath) + ".smart.jpg");

            using (var canvas = new Bitmap(screenBounds.Width, screenBounds.Height))
            using (var graphics = Graphics.FromImage(canvas))
            {
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.Clear(Color.Black);

                DrawDimmedBackground(graphics, sourceImage, screenBounds);
                DrawPortraitCopies(graphics, sourceImage, screenBounds);

                canvas.Save(targetPath, ImageFormat.Jpeg);
            }

            return targetPath;
        }

        private static void DrawDimmedBackground(Graphics graphics, Image sourceImage, Rectangle screenBounds)
        {
            var sourceAspect = sourceImage.Width / (double)sourceImage.Height;
            var screenAspect = screenBounds.Width / (double)screenBounds.Height;

            Rectangle drawRect;
            if (sourceAspect > screenAspect)
            {
                var height = screenBounds.Height;
                var width = (int)Math.Round(height * sourceAspect);
                drawRect = new Rectangle((screenBounds.Width - width) / 2, 0, width, height);
            }
            else
            {
                var width = screenBounds.Width;
                var height = (int)Math.Round(width / sourceAspect);
                drawRect = new Rectangle(0, (screenBounds.Height - height) / 2, width, height);
            }

            graphics.DrawImage(sourceImage, drawRect);
            using (var overlayBrush = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
            {
                graphics.FillRectangle(overlayBrush, screenBounds);
            }
        }

        private static void DrawPortraitCopies(Graphics graphics, Image sourceImage, Rectangle screenBounds)
        {
            var sourceAspect = sourceImage.Width / (double)sourceImage.Height;
            var copies = sourceAspect < 0.62 ? 3 : 2;
            var margin = (int)Math.Round(screenBounds.Width * 0.04);
            var spacing = (int)Math.Round(screenBounds.Width * 0.02);
            var availableWidth = screenBounds.Width - (margin * 2) - (spacing * (copies - 1));
            var maxWidthPerCopy = availableWidth / copies;
            var maxHeight = (int)Math.Round(screenBounds.Height * 0.84);

            var targetWidth = Math.Min(maxWidthPerCopy, (int)Math.Round(maxHeight * sourceAspect));
            var targetHeight = (int)Math.Round(targetWidth / sourceAspect);

            if (targetHeight > maxHeight)
            {
                targetHeight = maxHeight;
                targetWidth = (int)Math.Round(targetHeight * sourceAspect);
            }

            var totalWidth = (targetWidth * copies) + (spacing * (copies - 1));
            var startX = (screenBounds.Width - totalWidth) / 2;
            var startY = (screenBounds.Height - targetHeight) / 2;

            using (var shadowBrush = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
            {
                for (var i = 0; i < copies; i++)
                {
                    var x = startX + i * (targetWidth + spacing);
                    var targetRect = new Rectangle(x, startY, targetWidth, targetHeight);
                    var shadowRect = new Rectangle(targetRect.X + 8, targetRect.Y + 8, targetRect.Width, targetRect.Height);

                    graphics.FillRectangle(shadowBrush, shadowRect);
                    graphics.DrawImage(sourceImage, targetRect);
                }
            }
        }
    }
}
