using System;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    internal sealed class ApplicationController : IDisposable
    {
        private readonly Scheduler _scheduler;
        private readonly StartupService _startupService;
        private readonly ApodWorkflowService _workflowService;
        private readonly ApodCalendarStateService _calendarStateService;
        private bool _isInitialized;

        public ApplicationController()
        {
            _scheduler = new Scheduler();
            _startupService = new StartupService();
            _workflowService = new ApodWorkflowService();
            _calendarStateService = new ApodCalendarStateService(_workflowService);
        }

        public Scheduler Scheduler
        {
            get { return _scheduler; }
        }

        public void Initialize()
        {
            if (_isInitialized)
                return;

            ApplyRuntimeSettings();
            ConfigureScheduler(GetSettings());
            _isInitialized = true;
        }

        public ApplicationSettingsSnapshot GetSettings()
        {
            return new ApplicationSettingsSnapshot
            {
                TrayDoubleClickAction = Properties.Settings.Default.TrayDoubleClickAction,
                WallpaperStyleIndex = Properties.Settings.Default.StyleComboBox,
                AutoRefreshEnabled = Properties.Settings.Default.AutoRefreshEnabled,
                StartWithWindows = Properties.Settings.Default.StartWithWindows,
                NasaApiKey = Properties.Settings.Default.NasaApiKey,
                ImagesDirectoryPath = Properties.Settings.Default.ImagesDirectoryPath,
                LastAutoRefreshRunDate = Properties.Settings.Default.LastAutoRefreshRunDate,
            };
        }

        public void SaveSettings(ApplicationSettingsSnapshot settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            Properties.Settings.Default.TrayDoubleClickAction = settings.TrayDoubleClickAction;
            Properties.Settings.Default.StyleComboBox = settings.WallpaperStyleIndex;
            Properties.Settings.Default.AutoRefreshEnabled = settings.AutoRefreshEnabled;
            Properties.Settings.Default.StartWithWindows = settings.StartWithWindows;
            Properties.Settings.Default.ImagesDirectoryPath = Normalize(settings.ImagesDirectoryPath);
            Properties.Settings.Default.LastAutoRefreshRunDate = Normalize(settings.LastAutoRefreshRunDate);
            Properties.Settings.Default.NasaApiKey = string.IsNullOrWhiteSpace(settings.NasaApiKey)
                ? "DEMO_KEY"
                : settings.NasaApiKey.Trim();

            Properties.Settings.Default.Save();

            ApplyRuntimeSettings();
            _calendarStateService.Clear();
            ConfigureScheduler(settings);
            _startupService.SetStartWithWindows(settings.StartWithWindows);
        }

        public void UpdateSessionImagesDirectory(string path)
        {
            FileStorage.SetSessionImagesDirectory(path);
        }

        public ApodWorkflowResult LoadDay(DateTime date, bool forceRefresh = false)
        {
            var result = _workflowService.LoadDay(date, forceRefresh);
            _calendarStateService.Clear();
            return result;
        }

        public Task<ApodWorkflowResult> LoadDayAsync(DateTime date, bool forceRefresh = false)
        {
            return LoadDayAsyncInternal(date, forceRefresh);
        }

        public ApodWorkflowResult DownloadDay(DateTime date, bool forceRefresh = false)
        {
            var result = _workflowService.DownloadDay(date, forceRefresh);
            _calendarStateService.Clear();
            return result;
        }

        public Task<ApodWorkflowResult> DownloadDayAsync(DateTime date, bool forceRefresh = false)
        {
            return DownloadDayAsyncInternal(date, forceRefresh);
        }

        public ApodWorkflowResult ApplyDay(DateTime date, WallpaperStyle style, bool forceRefresh = false)
        {
            var result = _workflowService.ApplyDay(date, style, forceRefresh);
            _calendarStateService.Clear();
            return result;
        }

        public Task<ApodWorkflowResult> ApplyDayAsync(DateTime date, WallpaperStyle style, bool forceRefresh = false)
        {
            return ApplyDayAsyncInternal(date, style, forceRefresh);
        }

        public ApodWorkflowResult ApplyLatestPublished(WallpaperStyle style, bool forceRefresh = false)
        {
            var result = _workflowService.ApplyLatestPublished(style, forceRefresh);
            _calendarStateService.Clear();
            return result;
        }

        public Task<ApodWorkflowResult> ApplyLatestPublishedAsync(WallpaperStyle style, bool forceRefresh = false)
        {
            return ApplyLatestPublishedAsyncInternal(style, forceRefresh);
        }

        public string GetPostUrl(DateTime date)
        {
            return _workflowService.GetPostUrl(date);
        }

        public DateTime GetLatestPublishedDate()
        {
            return _workflowService.GetLatestPublishedDate();
        }

        public Task<DateTime> GetLatestPublishedDateAsync()
        {
            return _workflowService.GetLatestPublishedDateAsync();
        }

        public ApodCalendarMonthState GetCalendarMonthState(DateTime month, bool refreshMissingDates)
        {
            return _calendarStateService.GetMonthState(month, refreshMissingDates);
        }

        public Task<ApodCalendarMonthState> GetCalendarMonthStateAsync(DateTime month, bool refreshMissingDates)
        {
            return Task.Run(() => _calendarStateService.GetMonthState(month, refreshMissingDates));
        }

        public async Task RefreshLocalImageIndexAsync()
        {
            await _workflowService.RefreshLocalImageIndexAsync().ConfigureAwait(false);
            _calendarStateService.Clear();
        }

        public bool ShouldApplyOnTrayDoubleClick()
        {
            return Properties.Settings.Default.TrayDoubleClickAction;
        }

        public WallpaperStyle GetSelectedWallpaperStyle()
        {
            return (WallpaperStyle)Properties.Settings.Default.StyleComboBox;
        }

        public void Dispose()
        {
            _scheduler.Dispose();
        }

        private void ApplyRuntimeSettings()
        {
            RuntimeSettingsSync.ApplyCurrentSettings();
            FileStorage.SetSessionImagesDirectory(Properties.Settings.Default.ImagesDirectoryPath);
        }

        private void ConfigureScheduler(ApplicationSettingsSnapshot settings)
        {
            if (!settings.AutoRefreshEnabled)
            {
                _scheduler.Stop();
                return;
            }

            _scheduler.PollingInterval = ResolveSchedulerPollingInterval(settings);
            _scheduler.Start(RunScheduledWallpaperUpdate);
        }

        private async Task<ApodWorkflowResult> LoadDayAsyncInternal(DateTime date, bool forceRefresh)
        {
            var result = await _workflowService.LoadDayAsync(date, forceRefresh).ConfigureAwait(false);
            _calendarStateService.Clear();
            return result;
        }

        private async Task<ApodWorkflowResult> DownloadDayAsyncInternal(DateTime date, bool forceRefresh)
        {
            var result = await _workflowService.DownloadDayAsync(date, forceRefresh).ConfigureAwait(false);
            _calendarStateService.Clear();
            return result;
        }

        private async Task<ApodWorkflowResult> ApplyDayAsyncInternal(DateTime date, WallpaperStyle style, bool forceRefresh)
        {
            var result = await _workflowService.ApplyDayAsync(date, style, forceRefresh).ConfigureAwait(false);
            _calendarStateService.Clear();
            return result;
        }

        private async Task<ApodWorkflowResult> ApplyLatestPublishedAsyncInternal(WallpaperStyle style, bool forceRefresh)
        {
            var result = await _workflowService.ApplyLatestPublishedAsync(style, forceRefresh).ConfigureAwait(false);
            _calendarStateService.Clear();
            return result;
        }

        private void RunScheduledWallpaperUpdate()
        {
            try
            {
                var settings = GetSettings();
                if (!settings.AutoRefreshEnabled)
                    return;

                var now = DateTime.Now;
                DateTime lastRunDate;
                if (DateTime.TryParse(settings.LastAutoRefreshRunDate, out lastRunDate) && lastRunDate.Date == now.Date)
                    return;

                var localToday = now.Date;
                var result = ApplyDay(localToday, (WallpaperStyle)settings.WallpaperStyleIndex, true);
                if (!result.IsSuccess)
                    return;

                if (!result.ResolvedDate.HasValue || result.ResolvedDate.Value.Date != localToday)
                    return;

                Properties.Settings.Default.LastAutoRefreshRunDate = now.Date.ToString("yyyy-MM-dd");
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Scheduled wallpaper update failed.", ex);
            }
        }

        private static TimeSpan ResolveSchedulerPollingInterval(ApplicationSettingsSnapshot settings)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.NasaApiKey) || string.Equals(settings.NasaApiKey.Trim(), "DEMO_KEY", StringComparison.OrdinalIgnoreCase))
                return TimeSpan.FromHours(1);

            return TimeSpan.FromMinutes(5);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
