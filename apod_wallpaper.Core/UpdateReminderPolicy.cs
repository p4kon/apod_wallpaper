using System;
using System.Globalization;

namespace apod_wallpaper
{
    public enum CachedUpdateAvailabilityKind
    {
        None,
        UpdateAvailable,
    }

    public sealed class CachedUpdateAvailability
    {
        public static readonly CachedUpdateAvailability None = new CachedUpdateAvailability(CachedUpdateAvailabilityKind.None, string.Empty, string.Empty, string.Empty);

        public CachedUpdateAvailability(CachedUpdateAvailabilityKind kind, string currentVersion, string latestVersion, string releaseUrl)
        {
            Kind = kind;
            CurrentVersion = currentVersion ?? string.Empty;
            LatestVersion = latestVersion ?? string.Empty;
            ReleaseUrl = releaseUrl ?? string.Empty;
        }

        public CachedUpdateAvailabilityKind Kind { get; }

        public string CurrentVersion { get; }

        public string LatestVersion { get; }

        public string ReleaseUrl { get; }
    }

    public static class UpdateReminderPolicy
    {
        public static readonly TimeSpan AutomaticCheckInterval = TimeSpan.FromDays(1);
        public static readonly TimeSpan FailedAutomaticCheckRetryInterval = TimeSpan.FromHours(1);
        public static readonly TimeSpan ReminderCooldown = TimeSpan.FromDays(5);

        public static bool ShouldRunUpdateCheck(ApplicationSettingsSnapshot settings, DateTime nowUtc, bool automatic, bool forceCheck)
        {
            if (!automatic)
                return true;

            if (settings == null || !settings.AutoCheckUpdatesEnabled || settings.SuppressAutomaticUpdateReminder)
                return false;

            if (forceCheck)
                return true;

            return ShouldRunAutomaticCheck(settings, nowUtc);
        }

        public static bool ShouldRunAutomaticCheck(ApplicationSettingsSnapshot settings, DateTime nowUtc)
        {
            if (settings == null || !settings.AutoCheckUpdatesEnabled || settings.SuppressAutomaticUpdateReminder)
                return false;

            var lastAttemptUtc = ParseUtc(settings.LastAutomaticUpdateCheckUtc);
            if (!lastAttemptUtc.HasValue)
                return true;

            var lastFailedUtc = ParseUtc(settings.LastAutomaticUpdateCheckFailedUtc);
            var lastAttemptFailed = lastFailedUtc.HasValue && Math.Abs((lastFailedUtc.Value - lastAttemptUtc.Value).TotalSeconds) < 2;
            var interval = lastAttemptFailed ? FailedAutomaticCheckRetryInterval : AutomaticCheckInterval;
            return nowUtc - lastAttemptUtc.Value >= interval;
        }

        public static bool ShouldShowReminder(
            ApplicationSettingsSnapshot settings,
            string latestVersion,
            string currentVersion,
            DateTime nowUtc,
            bool reminderShownThisSession)
        {
            if (settings == null || reminderShownThisSession || !settings.AutoCheckUpdatesEnabled || settings.SuppressAutomaticUpdateReminder)
                return false;

            var normalizedLatest = UpdateCheckService.NormalizeVersionText(latestVersion);
            if (UpdateCheckService.CompareReleaseVersions(normalizedLatest, currentVersion) <= 0)
                return false;

            var lastReminderVersion = UpdateCheckService.NormalizeVersionText(settings.LastUpdateReminderVersion);
            if (string.IsNullOrWhiteSpace(settings.LastUpdateReminderVersion) || !string.Equals(lastReminderVersion, normalizedLatest, StringComparison.OrdinalIgnoreCase))
                return true;

            var lastReminderUtc = ParseUtc(settings.LastUpdateReminderShownUtc);
            return !lastReminderUtc.HasValue || nowUtc - lastReminderUtc.Value >= ReminderCooldown;
        }

        public static bool IsCachedUpdateAvailable(ApplicationSettingsSnapshot settings, string currentVersion)
        {
            return GetCachedUpdateAvailability(settings, currentVersion).Kind == CachedUpdateAvailabilityKind.UpdateAvailable;
        }

        public static CachedUpdateAvailability GetCachedUpdateAvailability(ApplicationSettingsSnapshot settings, string currentVersion)
        {
            var normalizedCurrent = UpdateCheckService.NormalizeVersionText(currentVersion);
            if (settings == null || string.IsNullOrWhiteSpace(settings.LastKnownLatestVersion))
                return new CachedUpdateAvailability(CachedUpdateAvailabilityKind.None, normalizedCurrent, string.Empty, string.Empty);

            var normalizedLatest = UpdateCheckService.NormalizeVersionText(settings.LastKnownLatestVersion);
            if (UpdateCheckService.CompareReleaseVersions(normalizedLatest, normalizedCurrent) <= 0)
                return new CachedUpdateAvailability(CachedUpdateAvailabilityKind.None, normalizedCurrent, normalizedLatest, settings.LastKnownLatestReleaseUrl);

            return new CachedUpdateAvailability(CachedUpdateAvailabilityKind.UpdateAvailable, normalizedCurrent, normalizedLatest, settings.LastKnownLatestReleaseUrl);
        }

        public static string FormatUtc(DateTime value)
        {
            return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }

        private static DateTime? ParseUtc(string value)
        {
            DateTime parsed;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
                return parsed.ToUniversalTime();

            return null;
        }
    }
}
