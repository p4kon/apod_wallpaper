using System;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    public interface IApodCalendarFacade
    {
        Task<OperationResult<ApodCalendarMonthState>> GetCalendarMonthStateAsync(DateTime month, bool refreshMissingDates, MonthRefreshMode refreshMode);
        Task<OperationResult<ApodCalendarYearState>> GetCalendarYearStateAsync(int year);
        Task<OperationResult<ApodPageAvailabilityProbeResult>> ProbeApodPageAvailabilityAsync(DateTime date);
    }
}
