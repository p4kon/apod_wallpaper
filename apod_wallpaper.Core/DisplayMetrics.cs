using System.Drawing;
using System.Runtime.InteropServices;

namespace apod_wallpaper
{
    internal static class DisplayMetrics
    {
        private const int SmCxScreen = 0;
        private const int SmCyScreen = 1;

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        public static Rectangle GetPrimaryScreenBounds()
        {
            var width = GetSystemMetrics(SmCxScreen);
            var height = GetSystemMetrics(SmCyScreen);

            if (width <= 0 || height <= 0)
                return new Rectangle(0, 0, 1920, 1080);

            return new Rectangle(0, 0, width, height);
        }
    }
}
