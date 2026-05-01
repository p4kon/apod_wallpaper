using System;
using System.Collections.Generic;
using System.Linq;

namespace apod_wallpaper
{
    internal sealed class ApodCalendarStateService
    {
        private readonly object _syncRoot = new object();
        private readonly ApodWorkflowService _workflowService;
        private readonly Dictionary<DateTime, ApodCalendarMonthState> _monthStates = new Dictionary<DateTime, ApodCalendarMonthState>();

        public ApodCalendarStateService(ApodWorkflowService workflowService)
        {
            _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
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

        public void Clear()
        {
            lock (_syncRoot)
            {
                _monthStates.Clear();
            }
        }
    }
}
