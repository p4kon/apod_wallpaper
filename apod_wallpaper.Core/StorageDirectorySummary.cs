namespace apod_wallpaper
{
    public sealed class StorageDirectorySummary
    {
        public string Path { get; set; }
        public int FileCount { get; set; }
        public long SizeBytes { get; set; }
        public bool Exists { get; set; }
    }
}
