using System;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    public interface IApodCalendarFacade
    {
        OperationResult<ApodCalendarMonthState> GetCalendarMonthState(DateTime month, bool refreshMissingDates);
        OperationResult<ApodCalendarMonthState> GetCalendarMonthState(DateTime month, bool refreshMissingDates, MonthRefreshMode refreshMode);
        Task<OperationResult<ApodCalendarMonthState>> GetCalendarMonthStateAsync(DateTime month, bool refreshMissingDates);
        Task<OperationResult<ApodCalendarMonthState>> GetCalendarMonthStateAsync(DateTime month, bool refreshMissingDates, MonthRefreshMode refreshMode);
    }
}
