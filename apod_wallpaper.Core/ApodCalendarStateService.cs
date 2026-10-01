using System;
using System.Collections.Generic;
using System.Linq;

namespace apod_wallpaper
{
    internal sealed class ApodCalendarStateService
    {
        private readonly object _syncRoot = new object();
        private readonly ApodWorkflowService _workflowService;
        private readonly Func<DateTime> _localToday;
        private DateTime _cacheDate;
        private readonly Dictionary<DateTime, ApodCalendarMonthState> _monthStates = new Dictionary<DateTime, ApodCalendarMonthState>();

        public ApodCalendarStateService(ApodWorkflowService workflowService, Func<DateTime> localToday = null)
        {
            _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
            _localToday = localToday ?? (() => DateTime.Today);
            _cacheDate = _localToday().Date;
        }

        public ApodCalendarMonthState GetMonthState(DateTime month, bool refreshMissingDates)
        {
            return GetMonthState(month, refreshMissingDates, MonthRefreshMode.Aggressive);
        }

        public ApodCalendarMonthState GetMonthState(DateTime month, bool refreshMissingDates, MonthRefreshMode refreshMode)
        {
            var monthKey = new DateTime(month.Year, month.Month, 1);

            lock (_syncRoot)
            {
                var today = _localToday().Date;
                if (_cacheDate != today)
                {
                    _monthStates.Clear();
                    _cacheDate = today;
                }
                ApodCalendarMonthState cachedState;
                if (!refreshMissingDates && _monthStates.TryGetValue(monthKey, out cachedState))
                    return cachedState;
            }

            var latestPublishedDate = refreshMissingDates
                ? _workflowService.GetLatestPublishedDate()
                : DateTime.UtcNow.Date;
            var monthStatus = _workflowService.GetMonthStatus(monthKey, refreshMissingDates, latestPublishedDate, refreshMode);
            var monthState = new ApodCalendarMonthState(
                monthKey,
                latestPublishedDate,
                monthStatus.Select(item => new ApodCalendarDayState
                {
                    Date = item.Date.Date,
                    IsKnown = item.IsKnown,
                    IsFuture = item.Date.Date > latestPublishedDate,
                    HasImage = item.HasImage,
                    IsLocalImageAvailable = item.IsLocalImageAvailable,
                    IsSelectable = item.IsSelectable,
                    MediaType = item.MediaType,
                    Source = item.Source,
                }));

            lock (_syncRoot)
            {
                _monthStates[monthKey] = monthState;
            }

            return monthState;
        }

        public ApodCalendarYearState GetYearState(int year)
        {
            var months = new List<ApodCalendarMonthState>();
            for (var month = 1; month <= 12; month++)
                months.Add(GetMonthState(new DateTime(year, month, 1), refreshMissingDates: false, MonthRefreshMode.Balanced));

            return new ApodCalendarYearState(year, months);
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _monthStates.Clear();
            }
        }
    }
}
