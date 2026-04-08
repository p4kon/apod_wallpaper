using System;

namespace apod_wallpaper
{
    public sealed class ApodWorkflowResult
    {
        public ApodWorkflowStatus Status { get; set; }
        public DateTime RequestedDate { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public DateTime? LatestPublishedDate { get; set; }
        public ApodEntry Entry { get; set; }
        public string PreviewLocation { get; set; }
        public string ImagePath { get; set; }
        public string PostUrl { get; set; }
        public string Message { get; set; }
        public bool IsLocalFile { get; set; }
        public bool DownloadedNow { get; set; }
        public ApodDataSource Source { get; set; }

        public bool IsSuccess
        {
            get { return Status == ApodWorkflowStatus.Success; }
        }
    }
}
