using System;

namespace apod_wallpaper
{
    public static class ApodCalendarAvailability
    {
        public static DateTime ResolveEffectiveLatestPublishedDate(DateTime latestPublishedDate, DateTime? transientAvailableDate)
        {
            if (!transientAvailableDate.HasValue)
                return latestPublishedDate.Date;

            var transientDate = transientAvailableDate.Value.Date;
            return transientDate > latestPublishedDate.Date ? transientDate : latestPublishedDate.Date;
        }

        public static bool ShouldThrottleProbe(DateTime today, DateTime? lastProbeDate, DateTime lastProbeUtc, DateTime nowUtc, TimeSpan throttle)
        {
            if (!lastProbeDate.HasValue)
                return false;

            if (lastProbeDate.Value.Date != today.Date)
                return false;

            return nowUtc - lastProbeUtc < throttle;
        }
    }
}
