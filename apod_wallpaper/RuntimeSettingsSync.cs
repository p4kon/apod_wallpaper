namespace apod_wallpaper
{
    internal static class RuntimeSettingsSync
    {
        public static void ApplyCurrentSettings()
        {
            AppRuntimeSettings.Configure(
                Properties.Settings.Default.NasaApiKey,
                Properties.Settings.Default.ImagesDirectoryPath);
        }
    }
}
