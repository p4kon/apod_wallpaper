namespace apod_wallpaper
{
    public sealed class DownloadProgressSnapshot
    {
        public long BytesReceived { get; set; }

        public long? TotalBytes { get; set; }

        public double BytesPerSecond { get; set; }
    }
}
