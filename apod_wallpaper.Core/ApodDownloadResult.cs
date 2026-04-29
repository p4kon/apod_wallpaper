namespace apod_wallpaper
{
    internal sealed class ApodDownloadResult
    {
        public ApodEntry Entry { get; set; }
        public string ImagePath { get; set; }
        public bool DownloadedNow { get; set; }
        public ApodDataSource Source { get; set; }
    }
}
