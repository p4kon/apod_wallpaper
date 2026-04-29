using System;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    public interface IApodWorkflowFacade
    {
        Task<OperationResult<ApodWorkflowResult>> LoadDayAsync(DateTime date, bool forceRefresh = false);
        Task<OperationResult<ApodWorkflowResult>> DownloadDayAsync(DateTime date, bool forceRefresh = false);
        Task<OperationResult<ApodWorkflowResult>> ApplyDayAsync(DateTime date, WallpaperStyle style, bool forceRefresh = false);
        Task<OperationResult<ApodWorkflowResult>> ApplyLatestPublishedAsync(WallpaperStyle style, bool forceRefresh = false);
        Task<OperationResult<string>> GetPostUrlAsync(DateTime date);
    }
}
