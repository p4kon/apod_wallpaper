using System;
using System.Collections.Generic;
using System.Linq;

namespace apod_wallpaper
{
    public sealed class ApodCalendarYearState
    {
        private readonly Dictionary<int, ApodCalendarMonthState> _monthsByNumber;

        public ApodCalendarYearState(int year, IEnumerable<ApodCalendarMonthState> months)
        {
            Year = year;
            Months = (months ?? Enumerable.Empty<ApodCalendarMonthState>()).ToList();
            _monthsByNumber = Months.ToDictionary(month => month.Month.Month, month => month);
        }

        public int Year { get; }
        public IReadOnlyList<ApodCalendarMonthState> Months { get; }

        public bool TryGetMonth(int monthNumber, out ApodCalendarMonthState monthState)
        {
            return _monthsByNumber.TryGetValue(monthNumber, out monthState);
        }
    }
}
