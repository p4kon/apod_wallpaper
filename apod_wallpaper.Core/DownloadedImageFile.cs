using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    internal sealed class DownloadedImageFile
    {
        private Bitmap bitmap;
        private readonly string image_url;
        public const string JpgExtension = ".jpg";
        public ImageFormat format;
        public string name;
        public string extension;

        public DownloadedImageFile(string image_url, string baseName)
        {
            this.image_url = image_url;
            this.extension = ResolveExtension(image_url);
            this.format = ResolveFormat(this.extension);
            this.name = baseName + this.extension;
        }

        public string FullPath
        {
            get
            {
                return FileStorage.GetImagePath(name);
            }
        }

        public void SaveImage(string filename, ImageFormat format)
        {
            if (bitmap != null)
            {
                var fullPath = FileStorage.GetImagePath(filename);
                bitmap.Save(fullPath, format);
            }
        }

        public void SaveImage()
        {
            SaveImage(name, format);
        }

        public void DownloadImage()
        {
            bitmap = Network.DownloadBitmap(image_url);
        }

        public async Task DownloadImageAsync()
        {
            bitmap = await Network.DownloadBitmapAsync(image_url).ConfigureAwait(false);
        }

        private static string ResolveExtension(string imageUrl)
        {
            Uri uri;
            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out uri))
                return JpgExtension;

            var extension = Path.GetExtension(uri.AbsolutePath);
            return FileStorage.NormalizeImageExtension(extension);
        }

        private static ImageFormat ResolveFormat(string extension)
        {
            switch (extension)
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
