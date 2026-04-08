using System;
using System.Collections.Generic;
using System.Linq;

namespace apod_wallpaper
{
    public sealed class ApodCalendarMonthState
    {
        private readonly Dictionary<DateTime, ApodCalendarDayState> _daysByDate;

        public ApodCalendarMonthState(DateTime month, DateTime latestPublishedDate, IEnumerable<ApodCalendarDayState> days)
        {
            Month = new DateTime(month.Year, month.Month, 1);
            LatestPublishedDate = latestPublishedDate.Date;
            Days = (days ?? Enumerable.Empty<ApodCalendarDayState>())
                .OrderBy(item => item.Date)
                .ToList()
                .AsReadOnly();
            _daysByDate = Days.ToDictionary(item => item.Date.Date, item => item);
        }

        public DateTime Month { get; }
        public DateTime LatestPublishedDate { get; }
        public IReadOnlyList<ApodCalendarDayState> Days { get; }

        public bool TryGetDay(DateTime date, out ApodCalendarDayState state)
        {
            return _daysByDate.TryGetValue(date.Date, out state);
        }
    }
}
