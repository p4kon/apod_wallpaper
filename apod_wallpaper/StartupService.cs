using Microsoft.Win32;

namespace apod_wallpaper
{
    internal sealed class StartupService
    {
        private const string RunRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "apod_wallpaper";

        public void SetStartWithWindows(bool enabled)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, true))
            {
                if (enabled)
                {
                    key.SetValue(AppName, "\"" + System.Windows.Forms.Application.ExecutablePath + "\"");
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
        }
    }
}
