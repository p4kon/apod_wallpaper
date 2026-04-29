using System;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    public interface IApodWorkflowFacade
    {
        OperationResult<ApodWorkflowResult> LoadDay(DateTime date, bool forceRefresh = false);
        Task<OperationResult<ApodWorkflowResult>> LoadDayAsync(DateTime date, bool forceRefresh = false);
        OperationResult<ApodWorkflowResult> DownloadDay(DateTime date, bool forceRefresh = false);
        Task<OperationResult<ApodWorkflowResult>> DownloadDayAsync(DateTime date, bool forceRefresh = false);
        OperationResult<ApodWorkflowResult> ApplyDay(DateTime date, WallpaperStyle style, bool forceRefresh = false);
        Task<OperationResult<ApodWorkflowResult>> ApplyDayAsync(DateTime date, WallpaperStyle style, bool forceRefresh = false);
        OperationResult<ApodWorkflowResult> ApplyLatestPublished(WallpaperStyle style, bool forceRefresh = false);
        Task<OperationResult<ApodWorkflowResult>> ApplyLatestPublishedAsync(WallpaperStyle style, bool forceRefresh = false);
        OperationResult<string> GetPostUrl(DateTime date);
    }
}
