namespace apod_wallpaper
{
    public sealed class ApplicationStoragePaths
    {
        public ApplicationStorageMode Mode { get; set; }
        public string ApplicationDataDirectory { get; set; }
        public string ImagesDirectory { get; set; }
        public string SmartImagesDirectory { get; set; }
        public string CacheDirectory { get; set; }
        public string LogsDirectory { get; set; }
        public string MetadataCacheFilePath { get; set; }
        public string SecretsDirectory { get; set; }
        public bool UsesCustomImagesDirectory { get; set; }
        public bool UsesExecutableImagesDirectory { get; set; }
    }
}
