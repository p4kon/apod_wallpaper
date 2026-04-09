using System.Drawing;
using System.IO;

namespace apod_wallpaper
{
    internal static class LocalImageValidator
    {
        public static bool IsUsableImageFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            if (!ImageFormatCatalog.IsSupportedImageExtension(Path.GetExtension(path)))
                return false;

            var fileInfo = new FileInfo(path);
            if (fileInfo.Length <= 0)
                return false;

            try
            {
                using (var image = Image.FromFile(path))
                {
                    return image.Width > 0 && image.Height > 0;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
