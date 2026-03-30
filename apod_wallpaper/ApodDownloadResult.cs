namespace apod_wallpaper
{
    public sealed class ApodDownloadResult
    {
        public ApodEntry Entry { get; set; }
        public string ImagePath { get; set; }
        public bool DownloadedNow { get; set; }
    }
}
