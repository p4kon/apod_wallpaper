using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    internal sealed class ApplicationController : IDisposable
    {
        private readonly Scheduler _scheduler;
        private readonly StartupService _startupService;
        private readonly ApodWorkflowService _workflowService;
        private readonly ApodCalendarStateService _calendarStateService;
        private readonly object _apiKeyValidationSync = new object();
        private int _scheduledUpdateInProgress;
        private bool _isInitialized;
        private Task<ApiKeyValidationState> _apiKeyValidationTask;
        private string _apiKeyValidationTaskKey;
        private DateTime _lastUnknownApiValidationUtc = DateTime.MinValue;
        private string _lastUnknownApiValidationKey;

        internal event EventHandler<WallpaperAppliedEventArgs> WallpaperApplied;

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
                NasaApiKeyValidationState = Properties.Settings.Default.NasaApiKeyValidationState,
                ImagesDirectoryPath = Properties.Settings.Default.ImagesDirectoryPath,
                LastAutoRefreshRunDate = Properties.Settings.Default.LastAutoRefreshRunDate,
                LastAutoRefreshAppliedDate = Properties.Settings.Default.LastAutoRefreshAppliedDate,
            };
        }

        public void SaveSettings(ApplicationSettingsSnapshot settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var previousAutoRefreshEnabled = Properties.Settings.Default.AutoRefreshEnabled;
            var previousApiKey = Normalize(Properties.Settings.Default.NasaApiKey);
            var normalizedApiKey = string.IsNullOrWhiteSpace(settings.NasaApiKey)
                ? "DEMO_KEY"
                : settings.NasaApiKey.Trim();
            var apiKeyChanged = !string.Equals(previousApiKey, normalizedApiKey, StringComparison.Ordinal);
            var effectiveValidationState = apiKeyChanged
                ? ApiKeyValidationState.Unknown.ToString()
                : (string.IsNullOrWhiteSpace(settings.NasaApiKeyValidationState)
                    ? NormalizeValidationState(Properties.Settings.Default.NasaApiKeyValidationState)
                    : NormalizeValidationState(settings.NasaApiKeyValidationState));

            Properties.Settings.Default.TrayDoubleClickAction = settings.TrayDoubleClickAction;
            Properties.Settings.Default.StyleComboBox = settings.WallpaperStyleIndex;
            Properties.Settings.Default.AutoRefreshEnabled = settings.AutoRefreshEnabled;
            Properties.Settings.Default.StartWithWindows = settings.StartWithWindows;
            Properties.Settings.Default.ImagesDirectoryPath = Normalize(settings.ImagesDirectoryPath);
            Properties.Settings.Default.NasaApiKey = normalizedApiKey;
            Properties.Settings.Default.NasaApiKeyValidationState = effectiveValidationState;
            Properties.Settings.Default.LastAutoRefreshRunDate = !previousAutoRefreshEnabled && settings.AutoRefreshEnabled
                ? string.Empty
                : Normalize(settings.LastAutoRefreshRunDate);
            Properties.Settings.Default.LastAutoRefreshAppliedDate = !previousAutoRefreshEnabled && settings.AutoRefreshEnabled
                ? string.Empty
                : Normalize(settings.LastAutoRefreshAppliedDate);

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
            EnsureApiKeyValidationIfNeeded(date, forceRefresh);
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
            EnsureApiKeyValidationIfNeeded(date, forceRefresh);
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
            EnsureApiKeyValidationIfNeeded(date, forceRefresh);
            var result = _workflowService.ApplyDay(date, style, forceRefresh);
            _calendarStateService.Clear();
            RaiseWallpaperApplied(result, false);
            return result;
        }

        public Task<ApodWorkflowResult> ApplyDayAsync(DateTime date, WallpaperStyle style, bool forceRefresh = false)
        {
            return ApplyDayAsyncInternal(date, style, forceRefresh);
        }

        public ApodWorkflowResult ApplyLatestPublished(WallpaperStyle style, bool forceRefresh = false)
        {
            EnsureApiKeyValidation();
            var result = _workflowService.ApplyLatestPublished(style, forceRefresh);
            _calendarStateService.Clear();
            RaiseWallpaperApplied(result, false);
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

        public DateTime GetLatestAvailableDate()
        {
            return _workflowService.GetLatestAvailableDate();
        }

        public Task<DateTime> GetLatestAvailableDateAsync()
        {
            return _workflowService.GetLatestAvailableDateAsync();
        }

        public ApiKeyValidationState GetApiKeyValidationState()
        {
            return ParseValidationState(Properties.Settings.Default.NasaApiKeyValidationState);
        }

        public async Task<ApiKeyValidationState> EnsureApiKeyValidationAsync()
        {
            var settings = GetSettings();
            if (string.IsNullOrWhiteSpace(settings.NasaApiKey) ||
                string.Equals(settings.NasaApiKey, "DEMO_KEY", StringComparison.OrdinalIgnoreCase))
            {
                if (GetApiKeyValidationState() != ApiKeyValidationState.Unknown)
                    SaveApiKeyValidationState(ApiKeyValidationState.Unknown);

                return ApiKeyValidationState.Unknown;
            }

            var currentState = ParseValidationState(settings.NasaApiKeyValidationState);
            if (currentState == ApiKeyValidationState.Invalid || currentState == ApiKeyValidationState.Valid)
                return currentState;

            var normalizedKey = Normalize(settings.NasaApiKey);
            lock (_apiKeyValidationSync)
            {
                if (!string.IsNullOrWhiteSpace(_lastUnknownApiValidationKey) &&
                    string.Equals(_lastUnknownApiValidationKey, normalizedKey, StringComparison.Ordinal) &&
                    DateTime.UtcNow - _lastUnknownApiValidationUtc < TimeSpan.FromMinutes(10))
                {
                    return ApiKeyValidationState.Unknown;
                }
            }

            Task<ApiKeyValidationState> validationTask;
            lock (_apiKeyValidationSync)
            {
                if (_apiKeyValidationTask != null &&
                    string.Equals(_apiKeyValidationTaskKey, normalizedKey, StringComparison.Ordinal))
                {
                    validationTask = _apiKeyValidationTask;
                }
                else
                {
                    validationTask = _workflowService.ValidateApiKeyAsync(settings.NasaApiKey);
                    _apiKeyValidationTask = validationTask;
                    _apiKeyValidationTaskKey = normalizedKey;
                }
            }

            var validationState = await validationTask.ConfigureAwait(false);
            lock (_apiKeyValidationSync)
            {
                if (ReferenceEquals(_apiKeyValidationTask, validationTask))
                {
                    _apiKeyValidationTask = null;
                    _apiKeyValidationTaskKey = null;
                }
            }

            if (validationState != ApiKeyValidationState.Unknown)
            {
                SaveApiKeyValidationState(validationState);
                lock (_apiKeyValidationSync)
                {
                    _lastUnknownApiValidationKey = null;
                    _lastUnknownApiValidationUtc = DateTime.MinValue;
                }
            }
            else
            {
                lock (_apiKeyValidationSync)
                {
                    _lastUnknownApiValidationKey = normalizedKey;
                    _lastUnknownApiValidationUtc = DateTime.UtcNow;
                }
            }

            return validationState;
        }

        public ApodCalendarMonthState GetCalendarMonthState(DateTime month, bool refreshMissingDates)
        {
            return GetCalendarMonthState(month, refreshMissingDates, MonthRefreshMode.Aggressive);
        }

        public ApodCalendarMonthState GetCalendarMonthState(DateTime month, bool refreshMissingDates, MonthRefreshMode refreshMode)
        {
            return _calendarStateService.GetMonthState(month, refreshMissingDates, refreshMode);
        }

        public Task<ApodCalendarMonthState> GetCalendarMonthStateAsync(DateTime month, bool refreshMissingDates)
        {
            return GetCalendarMonthStateAsync(month, refreshMissingDates, MonthRefreshMode.Aggressive);
        }

        public Task<ApodCalendarMonthState> GetCalendarMonthStateAsync(DateTime month, bool refreshMissingDates, MonthRefreshMode refreshMode)
        {
            return Task.Run(() => _calendarStateService.GetMonthState(month, refreshMissingDates, refreshMode));
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

        public DateTime GetPreferredDisplayDate()
        {
            var lastAppliedDate = ParseDate(Properties.Settings.Default.LastAutoRefreshAppliedDate);
            if (lastAppliedDate.HasValue && lastAppliedDate.Value <= DateTime.Today)
                return lastAppliedDate.Value;

            return DateTime.Today;
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
            await EnsureApiKeyValidationIfNeededAsync(date, forceRefresh).ConfigureAwait(false);
            var result = await _workflowService.LoadDayAsync(date, forceRefresh).ConfigureAwait(false);
            _calendarStateService.Clear();
            return result;
        }

        private async Task<ApodWorkflowResult> DownloadDayAsyncInternal(DateTime date, bool forceRefresh)
        {
            await EnsureApiKeyValidationIfNeededAsync(date, forceRefresh).ConfigureAwait(false);
            var result = await _workflowService.DownloadDayAsync(date, forceRefresh).ConfigureAwait(false);
            _calendarStateService.Clear();
            return result;
        }

        private async Task<ApodWorkflowResult> ApplyDayAsyncInternal(DateTime date, WallpaperStyle style, bool forceRefresh)
        {
            await EnsureApiKeyValidationIfNeededAsync(date, forceRefresh).ConfigureAwait(false);
            var result = await _workflowService.ApplyDayAsync(date, style, forceRefresh).ConfigureAwait(false);
            _calendarStateService.Clear();
            RaiseWallpaperApplied(result, false);
            return result;
        }

        private async Task<ApodWorkflowResult> ApplyLatestPublishedAsyncInternal(WallpaperStyle style, bool forceRefresh)
        {
            await EnsureApiKeyValidationAsync().ConfigureAwait(false);
            var result = await _workflowService.ApplyLatestPublishedAsync(style, forceRefresh).ConfigureAwait(false);
            _calendarStateService.Clear();
            RaiseWallpaperApplied(result, false);
            return result;
        }

        private void RunScheduledWallpaperUpdate()
        {
            if (Interlocked.Exchange(ref _scheduledUpdateInProgress, 1) == 1)
                return;

            try
            {
                var settings = GetSettings();
                if (!settings.AutoRefreshEnabled)
                    return;

                EnsureApiKeyValidation();

                var now = DateTime.Now;
                var localToday = now.Date;
                var latestPublishedDate = _workflowService.GetLatestPublishedDate().Date;
                var latestAvailableDate = _workflowService.GetLatestAvailableDate().Date;
                var lastRunDate = ParseDate(settings.LastAutoRefreshRunDate);
                var lastAppliedDate = ParseDate(settings.LastAutoRefreshAppliedDate);
                var latestPublicationIsForToday = latestPublishedDate >= localToday;

                if (latestPublicationIsForToday &&
                    lastRunDate.HasValue &&
                    lastRunDate.Value == localToday &&
                    lastAppliedDate.HasValue &&
                    lastAppliedDate.Value == latestAvailableDate)
                {
                    AppLogger.Info("Scheduler skipped because the latest available APOD was already applied today.");
                    return;
                }

                if (lastAppliedDate.HasValue && lastAppliedDate.Value == latestAvailableDate)
                {
                    AppLogger.Info("Scheduler checked APOD and found the same latest available image already applied: " + latestAvailableDate.ToString("yyyy-MM-dd") + ".");
                    return;
                }

                var result = _workflowService.ApplyLatestPublished((WallpaperStyle)settings.WallpaperStyleIndex, true);
                if (!result.IsSuccess)
                    return;

                _calendarStateService.Clear();
                var resolvedDate = result.ResolvedDate.HasValue
                    ? result.ResolvedDate.Value.Date
                    : latestAvailableDate;

                Properties.Settings.Default.LastAutoRefreshAppliedDate = resolvedDate.ToString("yyyy-MM-dd");
                Properties.Settings.Default.LastAutoRefreshRunDate = latestPublicationIsForToday && resolvedDate == latestPublishedDate
                    ? localToday.ToString("yyyy-MM-dd")
                    : string.Empty;
                Properties.Settings.Default.Save();
                RaiseWallpaperApplied(result, true);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Scheduled wallpaper update failed.", ex);
            }
            finally
            {
                Interlocked.Exchange(ref _scheduledUpdateInProgress, 0);
            }
        }

        private static TimeSpan ResolveSchedulerPollingInterval(ApplicationSettingsSnapshot settings)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.NasaApiKey) || string.Equals(settings.NasaApiKey.Trim(), "DEMO_KEY", StringComparison.OrdinalIgnoreCase))
                return TimeSpan.FromHours(1);

            return TimeSpan.FromMinutes(30);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string NormalizeValidationState(string value)
        {
            ApiKeyValidationState parsedState;
            return Enum.TryParse(value, true, out parsedState)
                ? parsedState.ToString()
                : ApiKeyValidationState.Unknown.ToString();
        }

        private static DateTime? ParseDate(string value)
        {
            DateTime parsedDate;
            return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate)
                ? parsedDate.Date
                : (DateTime?)null;
        }

        private static ApiKeyValidationState ParseValidationState(string value)
        {
            ApiKeyValidationState parsedState;
            return Enum.TryParse(value, true, out parsedState)
                ? parsedState
                : ApiKeyValidationState.Unknown;
        }

        private ApiKeyValidationState EnsureApiKeyValidation()
        {
            return EnsureApiKeyValidationAsync().GetAwaiter().GetResult();
        }

        private void EnsureApiKeyValidationIfNeeded(DateTime date, bool forceRefresh)
        {
            if (date.Date > DateTime.Today)
                return;

            if (!forceRefresh && _workflowService.HasUsableLocalImage(date))
                return;

            EnsureApiKeyValidation();
        }

        private Task EnsureApiKeyValidationIfNeededAsync(DateTime date, bool forceRefresh)
        {
            if (date.Date > DateTime.Today)
                return Task.CompletedTask;

            if (!forceRefresh && _workflowService.HasUsableLocalImage(date))
                return Task.CompletedTask;

            return EnsureApiKeyValidationAsync();
        }

        private void SaveApiKeyValidationState(ApiKeyValidationState validationState)
        {
            Properties.Settings.Default.NasaApiKeyValidationState = validationState.ToString();
            Properties.Settings.Default.Save();
            ApplyRuntimeSettings();
        }

        private void RaiseWallpaperApplied(ApodWorkflowResult result, bool automatic)
        {
            if (result == null || !result.IsSuccess)
                return;

            WallpaperApplied?.Invoke(this, new WallpaperAppliedEventArgs(result, automatic));
        }
    }
}
