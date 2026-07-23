using System;
using System.Globalization;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    internal sealed class UpdateCheckService
    {
        private static readonly Uri LatestReleaseUri = new Uri("https://api.github.com/repos/p4kon/apod_wallpaper/releases/latest");
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
        private static readonly HttpClient HttpClient = CreateHttpClient();

        public Task<UpdateCheckResult> CheckLatestReleaseAsync(string currentVersion)
        {
            return CheckLatestReleaseAsync(currentVersion, DefaultTimeout);
        }

        public async Task<UpdateCheckResult> CheckLatestReleaseAsync(string currentVersion, TimeSpan timeout)
        {
            var checkedAtUtc = DateTime.UtcNow;
            try
            {
                using (var cancellation = new CancellationTokenSource(timeout))
                using (var response = await HttpClient.GetAsync(LatestReleaseUri, HttpCompletionOption.ResponseHeadersRead, cancellation.Token).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                        return CreateFailure(currentVersion, "GitHub did not return latest release information.", checkedAtUtc);

                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(GitHubReleaseInfo));
                        var release = serializer.ReadObject(stream) as GitHubReleaseInfo;
                        if (release == null || string.IsNullOrWhiteSpace(release.TagName))
                            return CreateFailure(currentVersion, "GitHub latest release response was empty.", checkedAtUtc);

                        var latestVersion = NormalizeVersionText(release.TagName);
                        var comparison = CompareReleaseVersions(latestVersion, currentVersion);
                        return new UpdateCheckResult
                        {
                            Status = comparison > 0 ? UpdateCheckStatus.UpdateAvailable : UpdateCheckStatus.UpToDate,
                            CurrentVersion = NormalizeVersionText(currentVersion),
                            LatestVersion = latestVersion,
                            LatestReleaseUrl = release.HtmlUrl,
                            LatestReleaseName = release.Name,
                            CheckedAtUtc = checkedAtUtc,
                            Message = comparison > 0 ? "A newer APOD Wallpaper release is available." : "APOD Wallpaper is up to date.",
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Unable to check GitHub releases for updates.", ex);
                return CreateFailure(currentVersion, "Could not check for updates.", checkedAtUtc);
            }
        }

        internal static int CompareReleaseVersions(string latestVersion, string currentVersion)
        {
            var latest = ParseVersion(latestVersion);
            var current = ParseVersion(currentVersion);
            return latest.CompareTo(current);
        }

        internal static string NormalizeVersionText(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return "0.0.0";

            var normalized = version.Trim();
            if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(1);

            var metadataIndex = normalized.IndexOf('+');
            if (metadataIndex >= 0)
                normalized = normalized.Substring(0, metadataIndex);

            var prereleaseIndex = normalized.IndexOf('-');
            if (prereleaseIndex >= 0)
                normalized = normalized.Substring(0, prereleaseIndex);

            return string.IsNullOrWhiteSpace(normalized) ? "0.0.0" : normalized;
        }

        private static Version ParseVersion(string version)
        {
            var normalized = NormalizeVersionText(version);
            Version parsed;
            if (Version.TryParse(normalized, out parsed))
                return parsed;

            return new Version(0, 0, 0);
        }

        private static UpdateCheckResult CreateFailure(string currentVersion, string message, DateTime checkedAtUtc)
        {
            return new UpdateCheckResult
            {
                Status = UpdateCheckStatus.CouldNotCheck,
                CurrentVersion = NormalizeVersionText(currentVersion),
                CheckedAtUtc = checkedAtUtc,
                Message = message,
            };
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("APOD-Wallpaper/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            return client;
        }
    }
}
