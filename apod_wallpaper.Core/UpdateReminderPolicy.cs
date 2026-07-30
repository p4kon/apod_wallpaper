using System;
using System.Globalization;

namespace apod_wallpaper
{
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
            return settings != null
                && !string.IsNullOrWhiteSpace(settings.LastKnownLatestVersion)
                && UpdateCheckService.CompareReleaseVersions(settings.LastKnownLatestVersion, currentVersion) > 0;
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
