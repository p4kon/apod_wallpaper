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
                RefreshTime = Properties.Settings.Default.TimeRefresh,
                AutoRefreshEnabled = Properties.Settings.Default.AutoRefreshEnabled,
                StartWithWindows = Properties.Settings.Default.StartWithWindows,
                NasaApiKey = Properties.Settings.Default.NasaApiKey,
                ImagesDirectoryPath = Properties.Settings.Default.ImagesDirectoryPath,
            };
        }

        public void SaveSettings(ApplicationSettingsSnapshot settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            Properties.Settings.Default.TrayDoubleClickAction = settings.TrayDoubleClickAction;
            Properties.Settings.Default.StyleComboBox = settings.WallpaperStyleIndex;
            Properties.Settings.Default.TimeRefresh = settings.RefreshTime;
            Properties.Settings.Default.AutoRefreshEnabled = settings.AutoRefreshEnabled;
            Properties.Settings.Default.StartWithWindows = settings.StartWithWindows;
            Properties.Settings.Default.ImagesDirectoryPath = Normalize(settings.ImagesDirectoryPath);
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
            return _workflowService.LoadDay(date, forceRefresh);
        }

        public Task<ApodWorkflowResult> LoadDayAsync(DateTime date, bool forceRefresh = false)
        {
            return _workflowService.LoadDayAsync(date, forceRefresh);
        }

        public ApodWorkflowResult DownloadDay(DateTime date, bool forceRefresh = false)
        {
            return _workflowService.DownloadDay(date, forceRefresh);
        }

        public Task<ApodWorkflowResult> DownloadDayAsync(DateTime date, bool forceRefresh = false)
        {
            return _workflowService.DownloadDayAsync(date, forceRefresh);
        }

        public ApodWorkflowResult ApplyDay(DateTime date, WallpaperStyle style, bool forceRefresh = false)
        {
            return _workflowService.ApplyDay(date, style, forceRefresh);
        }

        public Task<ApodWorkflowResult> ApplyDayAsync(DateTime date, WallpaperStyle style, bool forceRefresh = false)
        {
            return _workflowService.ApplyDayAsync(date, style, forceRefresh);
        }

        public ApodWorkflowResult ApplyLatestPublished(WallpaperStyle style, bool forceRefresh = false)
        {
            return _workflowService.ApplyLatestPublished(style, forceRefresh);
        }

        public Task<ApodWorkflowResult> ApplyLatestPublishedAsync(WallpaperStyle style, bool forceRefresh = false)
        {
            return _workflowService.ApplyLatestPublishedAsync(style, forceRefresh);
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
            _scheduler.EveryHour = settings.RefreshTime.Hour;
            _scheduler.EveryMinute = settings.RefreshTime.Minute;
            _scheduler.EverySecond = settings.RefreshTime.Second;
            _scheduler.UpdateSchedule();

            if (!settings.AutoRefreshEnabled)
            {
                _scheduler.Stop();
                return;
            }

            _scheduler.Start(() => ApplyLatestPublished((WallpaperStyle)settings.WallpaperStyleIndex));
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
