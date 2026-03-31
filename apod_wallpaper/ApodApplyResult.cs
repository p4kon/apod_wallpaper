namespace apod_wallpaper
{
    public sealed class ApodApplyResult
    {
        public ApodEntry Entry { get; set; }
        public string ImagePath { get; set; }
        public bool DownloadedNow { get; set; }
        public ApodDataSource Source { get; set; }
    }
}
