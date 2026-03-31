namespace apod_wallpaper
{
    public static class AppRuntimeSettings
    {
        public static string NasaApiKey { get; private set; }
        public static string ImagesDirectoryPath { get; private set; }

        public static void Configure(string nasaApiKey, string imagesDirectoryPath)
        {
            NasaApiKey = Normalize(nasaApiKey);
            ImagesDirectoryPath = Normalize(imagesDirectoryPath);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
