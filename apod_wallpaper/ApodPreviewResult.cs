namespace apod_wallpaper
{
    public sealed class ApodPreviewResult
    {
        public ApodEntry Entry { get; set; }
        public string PreviewLocation { get; set; }
        public bool IsLocalFile { get; set; }
        public string PostUrl { get; set; }
        public ApodDataSource Source { get; set; }
    }
}
