using System;

namespace apod_wallpaper
{
    public sealed class UpdateCheckResult
    {
        public UpdateCheckStatus Status { get; set; }
        public string CurrentVersion { get; set; }
        public string LatestVersion { get; set; }
        public string LatestReleaseUrl { get; set; }
        public string LatestReleaseName { get; set; }
        public string Message { get; set; }
        public DateTime CheckedAtUtc { get; set; }

        public bool IsUpdateAvailable
        {
            get { return Status == UpdateCheckStatus.UpdateAvailable; }
        }
    }
}
