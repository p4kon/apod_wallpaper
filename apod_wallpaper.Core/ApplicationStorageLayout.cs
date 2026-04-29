namespace apod_wallpaper
{
    public static class ApplicationStorageLayout
    {
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
