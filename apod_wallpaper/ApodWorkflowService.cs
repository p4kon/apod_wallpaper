using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    internal sealed class ApodWorkflowService
    {
        private readonly ApodWallpaperService _wallpaperService = new ApodWallpaperService();

        public ApodWorkflowResult LoadDay(DateTime date, bool forceRefresh = false)
        {
            return Execute(date, () =>
            {
                var latestPublishedDate = _wallpaperService.GetLatestPublishedDate();
                if (date.Date > latestPublishedDate)
                {
                    return new ApodWorkflowResult
                    {
                        Status = ApodWorkflowStatus.Unavailable,
                        RequestedDate = date.Date,
                        LatestPublishedDate = latestPublishedDate,
                        Message = "NASA has not published APOD for this date yet.",
                    };
                }

                var preview = _wallpaperService.GetPreviewByDate(date, forceRefresh);
                if (preview.Entry == null || !preview.Entry.HasImage || string.IsNullOrWhiteSpace(preview.PreviewLocation))
                {
                    return new ApodWorkflowResult
                    {
                        Status = ApodWorkflowStatus.Unavailable,
                        RequestedDate = date.Date,
                        LatestPublishedDate = latestPublishedDate,
                        Entry = preview.Entry,
                        PostUrl = preview.PostUrl,
                        Message = "The selected APOD entry does not contain a downloadable image.",
                        Source = preview.Source,
                    };
                }

                return new ApodWorkflowResult
                {
                    Status = ApodWorkflowStatus.Success,
                    RequestedDate = date.Date,
                    ResolvedDate = ParseEntryDate(preview.Entry, date),
                    LatestPublishedDate = latestPublishedDate,
                    Entry = preview.Entry,
                    PreviewLocation = preview.PreviewLocation,
                    PostUrl = preview.PostUrl,
                    IsLocalFile = preview.IsLocalFile,
                    Source = preview.Source,
                };
            });
        }

        public Task<ApodWorkflowResult> LoadDayAsync(DateTime date, bool forceRefresh = false)
        {
            return ExecuteAsync(date, async () =>
            {
                var latestPublishedDate = await _wallpaperService.GetLatestPublishedDateAsync().ConfigureAwait(false);
                if (date.Date > latestPublishedDate)
                {
                    return new ApodWorkflowResult
                    {
                        Status = ApodWorkflowStatus.Unavailable,
                        RequestedDate = date.Date,
                        LatestPublishedDate = latestPublishedDate,
                        Message = "NASA has not published APOD for this date yet.",
                    };
                }

                var preview = await _wallpaperService.GetPreviewByDateAsync(date, forceRefresh).ConfigureAwait(false);
                if (preview.Entry == null || !preview.Entry.HasImage || string.IsNullOrWhiteSpace(preview.PreviewLocation))
                {
                    return new ApodWorkflowResult
                    {
                        Status = ApodWorkflowStatus.Unavailable,
                        RequestedDate = date.Date,
                        LatestPublishedDate = latestPublishedDate,
                        Entry = preview.Entry,
                        PostUrl = preview.PostUrl,
                        Message = "The selected APOD entry does not contain a downloadable image.",
                        Source = preview.Source,
                    };
                }

                return new ApodWorkflowResult
                {
                    Status = ApodWorkflowStatus.Success,
                    RequestedDate = date.Date,
                    ResolvedDate = ParseEntryDate(preview.Entry, date),
                    LatestPublishedDate = latestPublishedDate,
                    Entry = preview.Entry,
                    PreviewLocation = preview.PreviewLocation,
                    PostUrl = preview.PostUrl,
                    IsLocalFile = preview.IsLocalFile,
                    Source = preview.Source,
                };
            });
        }

        public ApodWorkflowResult DownloadDay(DateTime date, bool forceRefresh = false)
        {
            return Execute(date, () =>
            {
                var download = _wallpaperService.DownloadImageByDate(date, forceRefresh);
                return new ApodWorkflowResult
                {
                    Status = ApodWorkflowStatus.Success,
                    RequestedDate = date.Date,
                    ResolvedDate = ParseEntryDate(download.Entry, date),
                    Entry = download.Entry,
                    ImagePath = download.ImagePath,
                    PreviewLocation = download.ImagePath,
                    PostUrl = _wallpaperService.GetPostUrl(date),
                    DownloadedNow = download.DownloadedNow,
                    IsLocalFile = true,
                    Source = download.Source,
                };
            });
        }

        public Task<ApodWorkflowResult> DownloadDayAsync(DateTime date, bool forceRefresh = false)
        {
            return ExecuteAsync(date, async () =>
            {
                var download = await _wallpaperService.DownloadImageByDateAsync(date, forceRefresh).ConfigureAwait(false);
                return new ApodWorkflowResult
                {
                    Status = ApodWorkflowStatus.Success,
                    RequestedDate = date.Date,
                    ResolvedDate = ParseEntryDate(download.Entry, date),
                    Entry = download.Entry,
                    ImagePath = download.ImagePath,
                    PreviewLocation = download.ImagePath,
                    PostUrl = _wallpaperService.GetPostUrl(date),
                    DownloadedNow = download.DownloadedNow,
                    IsLocalFile = true,
                    Source = download.Source,
                };
            });
        }

        public ApodWorkflowResult ApplyDay(DateTime date, WallpaperStyle style, bool forceRefresh = false)
        {
            return Execute(date, () =>
            {
                var apply = _wallpaperService.ApplyWallpaperByDate(date, style, forceRefresh);
                return new ApodWorkflowResult
                {
                    Status = ApodWorkflowStatus.Success,
                    RequestedDate = date.Date,
                    ResolvedDate = ParseEntryDate(apply.Entry, date),
                    Entry = apply.Entry,
                    ImagePath = apply.ImagePath,
                    PreviewLocation = apply.ImagePath,
                    PostUrl = _wallpaperService.GetPostUrl(date),
                    DownloadedNow = apply.DownloadedNow,
                    IsLocalFile = true,
                    Source = apply.Source,
                };
            });
        }

        public Task<ApodWorkflowResult> ApplyDayAsync(DateTime date, WallpaperStyle style, bool forceRefresh = false)
        {
            return ExecuteAsync(date, async () =>
            {
                var apply = await _wallpaperService.ApplyWallpaperByDateAsync(date, style, forceRefresh).ConfigureAwait(false);
                return new ApodWorkflowResult
                {
                    Status = ApodWorkflowStatus.Success,
                    RequestedDate = date.Date,
                    ResolvedDate = ParseEntryDate(apply.Entry, date),
                    Entry = apply.Entry,
                    ImagePath = apply.ImagePath,
                    PreviewLocation = apply.ImagePath,
                    PostUrl = _wallpaperService.GetPostUrl(date),
                    DownloadedNow = apply.DownloadedNow,
                    IsLocalFile = true,
                    Source = apply.Source,
                };
            });
        }

        public ApodWorkflowResult ApplyLatestPublished(WallpaperStyle style, bool forceRefresh = false)
        {
            return Execute(DateTime.UtcNow.Date, () =>
            {
                var apply = _wallpaperService.ApplyLatestPublishedWallpaper(style, forceRefresh);
                var resolvedDate = ParseEntryDate(apply.Entry, DateTime.UtcNow.Date);

                return new ApodWorkflowResult
                {
                    Status = ApodWorkflowStatus.Success,
                    RequestedDate = DateTime.UtcNow.Date,
                    ResolvedDate = resolvedDate,
                    LatestPublishedDate = resolvedDate,
                    Entry = apply.Entry,
                    ImagePath = apply.ImagePath,
                    PreviewLocation = apply.ImagePath,
                    PostUrl = _wallpaperService.GetPostUrl(resolvedDate ?? DateTime.UtcNow.Date),
                    DownloadedNow = apply.DownloadedNow,
                    IsLocalFile = true,
                    Source = apply.Source,
                };
            });
        }

        public Task<ApodWorkflowResult> ApplyLatestPublishedAsync(WallpaperStyle style, bool forceRefresh = false)
        {
            return ExecuteAsync(DateTime.UtcNow.Date, async () =>
            {
                var apply = await _wallpaperService.ApplyLatestPublishedWallpaperAsync(style, forceRefresh).ConfigureAwait(false);
                var resolvedDate = ParseEntryDate(apply.Entry, DateTime.UtcNow.Date);

                return new ApodWorkflowResult
                {
                    Status = ApodWorkflowStatus.Success,
                    RequestedDate = DateTime.UtcNow.Date,
                    ResolvedDate = resolvedDate,
                    LatestPublishedDate = resolvedDate,
                    Entry = apply.Entry,
                    ImagePath = apply.ImagePath,
                    PreviewLocation = apply.ImagePath,
                    PostUrl = _wallpaperService.GetPostUrl(resolvedDate ?? DateTime.UtcNow.Date),
                    DownloadedNow = apply.DownloadedNow,
                    IsLocalFile = true,
                    Source = apply.Source,
                };
            });
        }

        public IReadOnlyList<ApodDayAvailability> GetMonthStatus(DateTime month, bool refreshMissingDates)
        {
            return _wallpaperService.GetMonthStatus(month, refreshMissingDates);
        }

        public Task<IReadOnlyList<ApodDayAvailability>> GetMonthStatusAsync(DateTime month, bool refreshMissingDates)
        {
            return _wallpaperService.GetMonthStatusAsync(month, refreshMissingDates);
        }

        public string GetPostUrl(DateTime date)
        {
            return _wallpaperService.GetPostUrl(date);
        }

        public DateTime GetLatestPublishedDate()
        {
            return _wallpaperService.GetLatestPublishedDate();
        }

        public Task<DateTime> GetLatestPublishedDateAsync()
        {
            return _wallpaperService.GetLatestPublishedDateAsync();
        }

        public Task RefreshLocalImageIndexAsync()
        {
            return _wallpaperService.RefreshLocalImageIndexAsync();
        }

        private static ApodWorkflowResult Execute(DateTime requestedDate, Func<ApodWorkflowResult> action)
        {
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                AppLogger.Warn("APOD workflow failed for " + requestedDate.ToString("yyyy-MM-dd") + ".", ex);
                return new ApodWorkflowResult
                {
                    Status = ApodWorkflowStatus.Failed,
                    RequestedDate = requestedDate.Date,
                    Message = ApodErrorTranslator.ToUserMessage(ex),
                    Source = ApodDataSource.Unknown,
                };
            }
        }

        private static async Task<ApodWorkflowResult> ExecuteAsync(DateTime requestedDate, Func<Task<ApodWorkflowResult>> action)
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("APOD workflow failed for " + requestedDate.ToString("yyyy-MM-dd") + ".", ex);
                return new ApodWorkflowResult
                {
                    Status = ApodWorkflowStatus.Failed,
                    RequestedDate = requestedDate.Date,
                    Message = ApodErrorTranslator.ToUserMessage(ex),
                    Source = ApodDataSource.Unknown,
                };
            }
        }

        private static DateTime? ParseEntryDate(ApodEntry entry, DateTime fallbackDate)
        {
            DateTime parsedDate;
            return entry != null && DateTime.TryParse(entry.Date, out parsedDate)
                ? parsedDate.Date
                : fallbackDate.Date;
        }
    }
}
