namespace apod_wallpaper
{
    public sealed class StorageSummary
    {
        public ApplicationStoragePaths Paths { get; set; }
        public StorageDirectorySummary Images { get; set; }
        public StorageDirectorySummary SmartImages { get; set; }
        public StorageDirectorySummary Cache { get; set; }
        public StorageDirectorySummary Logs { get; set; }
        public StorageDirectorySummary ApplicationData { get; set; }
        public int DownloadedImageCount { get; set; }
        public long DownloadedImageSizeBytes { get; set; }
    }
}
