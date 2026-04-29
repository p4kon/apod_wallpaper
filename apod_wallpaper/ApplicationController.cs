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

        internal Scheduler Scheduler
        {
            get { return _scheduler; }
        }

        public OperationResult<ApplicationSettingsSnapshot> Initialize()
        {
            return ExecuteOperation(() =>
            {
                if (!_isInitialized)
                {
                    ApplyRuntimeSettings();
                    ConfigureScheduler(BuildSettingsSnapshot());
                    _isInitialized = true;
                }

                return BuildSettingsSnapshot();
            }, OperationErrorCode.InitializationFailed, "Unable to initialize the application controller.");
        }

        public OperationResult<ApplicationSettingsSnapshot> GetSettings()
        {
            return ExecuteOperation(BuildSettingsSnapshot, OperationErrorCode.SettingsReadFailed, "Unable to load saved application settings.");
        }

        public OperationResult<ApplicationSettingsSnapshot> SaveSettings(ApplicationSettingsSnapshot settings)
        {
            return ExecuteOperation(() =>
            {
                SaveSettingsCore(settings);
                return BuildSettingsSnapshot();
            }, OperationErrorCode.SettingsWriteFailed, "Unable to save application settings.");
        }

        public OperationResult<string> UpdateSessionImagesDirectory(string path)
        {
            return ExecuteOperation(() =>
            {
                FileStorage.SetSessionImagesDirectory(path);
                return FileStorage.ImagesDirectory;
            }, OperationErrorCode.StateUpdateFailed, "Unable to update the active images directory for this session.");
        }

        internal DateTime GetLatestPublishedDate()
        {
            return _workflowService.GetLatestPublishedDate();
        }

        internal Task<DateTime> GetLatestPublishedDateAsync()
        {
            return _workflowService.GetLatestPublishedDateAsync();
        }

        internal DateTime GetLatestAvailableDate()
        {
            return _workflowService.GetLatestAvailableDate();
        }

        internal Task<DateTime> GetLatestAvailableDateAsync()
        {
            return _workflowService.GetLatestAvailableDateAsync();
        }

        internal async Task<ApiKeyValidationState> EnsureApiKeyValidationAsync()
        {
            var settings = BuildSettingsSnapshot();
            if (string.IsNullOrWhiteSpace(settings.NasaApiKey) ||
                string.Equals(settings.NasaApiKey, "DEMO_KEY", StringComparison.OrdinalIgnoreCase))
            {
                if (GetApiKeyValidationStateCore() != ApiKeyValidationState.Unknown)
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

        public OperationResult<ApodWorkflowResult> LoadDay(DateTime date, bool forceRefresh = false)
        {
            return ExecuteOperation(() =>
            {
                EnsureApiKeyValidationIfNeeded(date, forceRefresh);
                var result = _workflowService.LoadDay(date, forceRefresh);
                _calendarStateService.Clear();
                return result;
            }, OperationErrorCode.WorkflowFailed, "Unable to load the requested APOD entry.");
        }

        public Task<OperationResult<ApodWorkflowResult>> LoadDayAsync(DateTime date, bool forceRefresh = false)
        {
            return ExecuteOperationAsync(async () =>
            {
                await EnsureApiKeyValidationIfNeededAsync(date, forceRefresh).ConfigureAwait(false);
                var result = await _workflowService.LoadDayAsync(date, forceRefresh).ConfigureAwait(false);
                _calendarStateService.Clear();
                return result;
            }, OperationErrorCode.WorkflowFailed, "Unable to load the requested APOD entry.");
        }

        public OperationResult<ApodWorkflowResult> DownloadDay(DateTime date, bool forceRefresh = false)
        {
            return ExecuteOperation(() =>
            {
                EnsureApiKeyValidationIfNeeded(date, forceRefresh);
                var result = _workflowService.DownloadDay(date, forceRefresh);
                _calendarStateService.Clear();
                return result;
            }, OperationErrorCode.WorkflowFailed, "Unable to download the requested APOD image.");
        }

        public Task<OperationResult<ApodWorkflowResult>> DownloadDayAsync(DateTime date, bool forceRefresh = false)
        {
            return ExecuteOperationAsync(async () =>
            {
                await EnsureApiKeyValidationIfNeededAsync(date, forceRefresh).ConfigureAwait(false);
                var result = await _workflowService.DownloadDayAsync(date, forceRefresh).ConfigureAwait(false);
                _calendarStateService.Clear();
                return result;
            }, OperationErrorCode.WorkflowFailed, "Unable to download the requested APOD image.");
        }

        public OperationResult<ApodWorkflowResult> ApplyDay(DateTime date, WallpaperStyle style, bool forceRefresh = false)
        {
            return ExecuteOperation(() =>
            {
                EnsureApiKeyValidationIfNeeded(date, forceRefresh);
                var result = _workflowService.ApplyDay(date, style, forceRefresh);
                _calendarStateService.Clear();
                RaiseWallpaperApplied(result, false);
                return result;
            }, OperationErrorCode.WorkflowFailed, "Unable to apply the requested APOD image as wallpaper.");
        }

        public Task<OperationResult<ApodWorkflowResult>> ApplyDayAsync(DateTime date, WallpaperStyle style, bool forceRefresh = false)
        {
            return ExecuteOperationAsync(async () =>
            {
                await EnsureApiKeyValidationIfNeededAsync(date, forceRefresh).ConfigureAwait(false);
                var result = await _workflowService.ApplyDayAsync(date, style, forceRefresh).ConfigureAwait(false);
                _calendarStateService.Clear();
                RaiseWallpaperApplied(result, false);
                return result;
            }, OperationErrorCode.WorkflowFailed, "Unable to apply the requested APOD image as wallpaper.");
        }

        public OperationResult<ApodWorkflowResult> ApplyLatestPublished(WallpaperStyle style, bool forceRefresh = false)
        {
            return ExecuteOperation(() =>
            {
                EnsureApiKeyValidation();
                var result = _workflowService.ApplyLatestPublished(style, forceRefresh);
                _calendarStateService.Clear();
                RaiseWallpaperApplied(result, false);
                return result;
            }, OperationErrorCode.WorkflowFailed, "Unable to apply the latest available APOD image.");
        }

        public Task<OperationResult<ApodWorkflowResult>> ApplyLatestPublishedAsync(WallpaperStyle style, bool forceRefresh = false)
        {
            return ExecuteOperationAsync(async () =>
            {
                await EnsureApiKeyValidationAsync().ConfigureAwait(false);
                var result = await _workflowService.ApplyLatestPublishedAsync(style, forceRefresh).ConfigureAwait(false);
                _calendarStateService.Clear();
                RaiseWallpaperApplied(result, false);
                return result;
            }, OperationErrorCode.WorkflowFailed, "Unable to apply the latest available APOD image.");
        }

        public OperationResult<string> GetPostUrl(DateTime date)
        {
            return ExecuteOperation(() => _workflowService.GetPostUrl(date), OperationErrorCode.WorkflowFailed, "Unable to resolve the NASA APOD page URL.");
        }

        public OperationResult<ApiKeyValidationState> GetApiKeyValidationState()
        {
            return ExecuteOperation(GetApiKeyValidationStateCore, OperationErrorCode.SettingsReadFailed, "Unable to read the current API key validation state.");
        }

        public OperationResult<ApodCalendarMonthState> GetCalendarMonthState(DateTime month, bool refreshMissingDates)
        {
            return GetCalendarMonthState(month, refreshMissingDates, MonthRefreshMode.Aggressive);
        }

        public OperationResult<ApodCalendarMonthState> GetCalendarMonthState(DateTime month, bool refreshMissingDates, MonthRefreshMode refreshMode)
        {
            return ExecuteOperation(
                () => _calendarStateService.GetMonthState(month, refreshMissingDates, refreshMode),
                OperationErrorCode.WorkflowFailed,
                "Unable to build calendar month state.");
        }

        public Task<OperationResult<ApodCalendarMonthState>> GetCalendarMonthStateAsync(DateTime month, bool refreshMissingDates)
        {
            return GetCalendarMonthStateAsync(month, refreshMissingDates, MonthRefreshMode.Aggressive);
        }

        public Task<OperationResult<ApodCalendarMonthState>> GetCalendarMonthStateAsync(DateTime month, bool refreshMissingDates, MonthRefreshMode refreshMode)
        {
            return ExecuteOperationAsync(
                () => Task.Run(() => _calendarStateService.GetMonthState(month, refreshMissingDates, refreshMode)),
                OperationErrorCode.WorkflowFailed,
                "Unable to build calendar month state.");
        }

        public Task<OperationResult> RefreshLocalImageIndexAsync()
        {
            return ExecuteOperationAsync(async () =>
            {
                await _workflowService.RefreshLocalImageIndexAsync().ConfigureAwait(false);
                _calendarStateService.Clear();
            }, OperationErrorCode.StorageFailed, "Unable to refresh the local image index.");
        }

        public OperationResult<bool> ShouldApplyOnTrayDoubleClick()
        {
            return ExecuteOperation(() => Properties.Settings.Default.TrayDoubleClickAction, OperationErrorCode.SettingsReadFailed, "Unable to read tray double-click behavior.");
        }

        public OperationResult<DateTime> GetPreferredDisplayDate()
        {
            return ExecuteOperation(() =>
            {
                var lastAppliedDate = ParseDate(Properties.Settings.Default.LastAutoRefreshAppliedDate);
                if (lastAppliedDate.HasValue && lastAppliedDate.Value <= DateTime.Today)
                    return lastAppliedDate.Value;

                return DateTime.Today;
            }, OperationErrorCode.SettingsReadFailed, "Unable to resolve the preferred display date.");
        }

        public OperationResult<WallpaperStyle> GetSelectedWallpaperStyle()
        {
            return ExecuteOperation(() => (WallpaperStyle)Properties.Settings.Default.StyleComboBox, OperationErrorCode.SettingsReadFailed, "Unable to read the selected wallpaper style.");
        }

        public OperationResult Shutdown()
        {
            return ExecuteOperation(() =>
            {
                ShutdownCore();
                return true;
            }, OperationErrorCode.ShutdownFailed, "Unable to shut down the application controller cleanly.");
        }

        void IDisposable.Dispose()
        {
            ShutdownCore();
        }

        private ApplicationSettingsSnapshot BuildSettingsSnapshot()
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

        private void SaveSettingsCore(ApplicationSettingsSnapshot settings)
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

            if (apiKeyChanged)
                ResetApiKeyValidationRuntimeState();

            ApplyRuntimeSettings();
            _calendarStateService.Clear();
            ConfigureScheduler(settings);
            _startupService.SetStartWithWindows(settings.StartWithWindows);
        }

        private ApiKeyValidationState GetApiKeyValidationStateCore()
        {
            return ParseValidationState(Properties.Settings.Default.NasaApiKeyValidationState);
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

        private void ShutdownCore()
        {
            _scheduler.Dispose();
        }

        private static OperationResult<T> ExecuteOperation<T>(Func<T> operation, OperationErrorCode errorCode, string failureMessage, bool retryable = false)
        {
            try
            {
                return OperationResult<T>.Success(operation());
            }
            catch (Exception ex)
            {
                AppLogger.Warn(failureMessage, ex);
                return OperationResult<T>.Failure(CreateOperationError(errorCode, failureMessage, ex, retryable));
            }
        }

        private static async Task<OperationResult<T>> ExecuteOperationAsync<T>(Func<Task<T>> operation, OperationErrorCode errorCode, string failureMessage, bool retryable = false)
        {
            try
            {
                return OperationResult<T>.Success(await operation().ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                AppLogger.Warn(failureMessage, ex);
                return OperationResult<T>.Failure(CreateOperationError(errorCode, failureMessage, ex, retryable));
            }
        }

        private static async Task<OperationResult> ExecuteOperationAsync(Func<Task> operation, OperationErrorCode errorCode, string failureMessage, bool retryable = false)
        {
            try
            {
                await operation().ConfigureAwait(false);
                return OperationResult.Success();
            }
            catch (Exception ex)
            {
                AppLogger.Warn(failureMessage, ex);
                return OperationResult.Failure(CreateOperationError(errorCode, failureMessage, ex, retryable));
            }
        }

        private void RunScheduledWallpaperUpdate()
        {
            if (Interlocked.Exchange(ref _scheduledUpdateInProgress, 1) == 1)
                return;

            try
            {
                var settings = BuildSettingsSnapshot();
                if (!settings.AutoRefreshEnabled)
                    return;

                var now = DateTime.Now;
                var localToday = now.Date;
                var lastRunDate = ParseDate(settings.LastAutoRefreshRunDate);
                var lastAppliedDate = ParseDate(settings.LastAutoRefreshAppliedDate);

                // Day-level short circuit: once scheduler has already finished successfully for local "today",
                // do not burn extra API/HTML checks until the next day.
                if (ShouldSkipSchedulerForToday(lastRunDate, lastAppliedDate, localToday))
                {
                    AppLogger.Info("Scheduler skipped because today's auto-check already completed.");
                    return;
                }

                EnsureApiKeyValidation();

                var latestPublishedDate = _workflowService.GetLatestPublishedDate().Date;
                var latestAvailableDate = _workflowService.GetLatestAvailableDate().Date;
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
                Properties.Settings.Default.LastAutoRefreshRunDate = localToday.ToString("yyyy-MM-dd");
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

        private void ResetApiKeyValidationRuntimeState()
        {
            lock (_apiKeyValidationSync)
            {
                _apiKeyValidationTask = null;
                _apiKeyValidationTaskKey = null;
                _lastUnknownApiValidationKey = null;
                _lastUnknownApiValidationUtc = DateTime.MinValue;
            }
        }

        private void RaiseWallpaperApplied(ApodWorkflowResult result, bool automatic)
        {
            if (result == null || !result.IsSuccess)
                return;

            WallpaperApplied?.Invoke(this, new WallpaperAppliedEventArgs(result, automatic));
        }

        internal static bool ShouldSkipSchedulerForToday(DateTime? lastRunDate, DateTime? lastAppliedDate, DateTime localToday)
        {
            return lastRunDate.HasValue &&
                   lastRunDate.Value == localToday.Date &&
                   lastAppliedDate.HasValue;
        }

        private static OperationError CreateOperationError(OperationErrorCode code, string message, Exception exception, bool retryable)
        {
            return new OperationError(code, message, retryable, exception != null ? exception.GetType().Name + ": " + exception.Message : null);
        }
    }
}
