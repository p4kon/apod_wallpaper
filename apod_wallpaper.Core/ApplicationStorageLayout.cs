namespace apod_wallpaper
{
    public static class ApplicationStorageLayout
    {
        public static void Configure(ApplicationStorageMode? modeOverride = null, string applicationDataDirectoryOverride = null)
        {
            FileStorage.Configure(modeOverride, applicationDataDirectoryOverride);
        }

        public static void ResetConfiguration()
        {
            FileStorage.Configure(null, null);
        }

        public static ApplicationStoragePaths GetStoragePaths()
        {
            return FileStorage.GetStoragePaths();
        }

        public static ApplicationStoragePaths EnsureStorageLayout()
        {
            return FileStorage.EnsureStorageLayout();
        }
    }
}
