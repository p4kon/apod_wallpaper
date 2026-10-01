using System;

namespace apod_wallpaper
{
    // Session-only publication confirmation. Does not own preview metadata or wallpaper state.
    public sealed class CalendarPublicationProbeState
    {
        private DateTime? _date;
        private DateTime _retryAtUtc;
        private int _failures;
        public DateTime? ConfirmedDate { get; private set; }

        public bool ShouldProbe(DateTime today, DateTime nowUtc)
        {
            return _date != today.Date || (ConfirmedDate != today.Date && nowUtc >= _retryAtUtc);
        }

        public void Complete(DateTime date, DateTime nowUtc, ApodPageAvailabilityProbeResult result)
        {
            date = date.Date;
            if (_date != date)
            {
                _date = date;
                _failures = 0;
                ConfirmedDate = null;
            }
            if (result != null && result.Date == date && result.IsAvailable)
            {
                ConfirmedDate = date;
                _failures = 0;
            }
            else
            {
                _failures++;
                _retryAtUtc = nowUtc.Add(result != null && result.IsUnavailable
                    ? TimeSpan.FromMinutes(5)
                    : TimeSpan.FromSeconds(_failures == 1 ? 15 : _failures == 2 ? 60 : 300));
            }
        }
    }

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
