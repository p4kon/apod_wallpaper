using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    public sealed class ApplicationController : IApplicationBackendFacade, IDisposable
    {
        private const string DemoApiKey = "DEMO_KEY";
        private readonly IApplicationSettingsStore _settingsStore;
        private readonly IUserSecretStore _secretStore;
        private readonly IStartupRegistrationService _startupRegistrationService;
        private readonly Scheduler _scheduler;
        private readonly ApodWorkflowService _workflowService;
        private readonly ApodCalendarStateService _calendarStateService;
        private readonly ApodPageAvailabilityProbe _pageAvailabilityProbe;
        private readonly FavoriteApodStore _favoriteStore;
        private readonly UpdateCheckService _updateCheckService;
        private readonly object _apiKeyValidationSync = new object();
        private int _scheduledUpdateInProgress;
        private bool _isInitialized;
        private Task<ApiKeyValidationState> _apiKeyValidationTask;
        private string _apiKeyValidationTaskKey;
        private DateTime _lastUnknownApiValidationUtc = DateTime.MinValue;
        private string _lastUnknownApiValidationKey;

        public event EventHandler<WallpaperAppliedEventArgs> WallpaperApplied;

        public ApplicationController(IApplicationSettingsStore settingsStore, IUserSecretStore secretStore, IStartupRegistrationService startupRegistrationService)
        {
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
            _startupRegistrationService = startupRegistrationService ?? throw new ArgumentNullException(nameof(startupRegistrationService));
            _scheduler = new Scheduler();
            _workflowService = new ApodWorkflowService();
            _calendarStateService = new ApodCalendarStateService(_workflowService);
            _pageAvailabilityProbe = new ApodPageAvailabilityProbe();
            _favoriteStore = new FavoriteApodStore();
            _updateCheckService = new UpdateCheckService();
        }

        internal Scheduler Scheduler
        {
            get { return _scheduler; }
        }

        public Task<OperationResult<ApplicationSettingsSnapshot>> InitializeAsync()
        {
            return Task.FromResult(ExecuteOperation(() =>
            {
                if (!_isInitialized)
                {
                    ApplyRuntimeSettings();
                    ConfigureScheduler(BuildSettingsSnapshot());
                    _isInitialized = true;
                }

                return BuildSettingsSnapshot();
            }, OperationErrorCode.InitializationFailed, "Unable to initialize the application controller."));
        }

        public Task<OperationResult<IEventSubscription>> SubscribeWallpaperAppliedAsync(EventHandler<WallpaperAppliedEventArgs> handler)
        {
            return Task.FromResult(ExecuteOperation<IEventSubscription>(() =>
            {
                if (handler == null)
                    throw new ArgumentNullException(nameof(handler));

                WallpaperApplied += handler;
                return new WallpaperAppliedSubscription(this, handler);
            }, OperationErrorCode.StateUpdateFailed, "Unable to subscribe to wallpaper applied events."));
        }

        public Task<OperationResult<ApplicationSettingsSnapshot>> GetSettingsAsync()
        {
            return Task.FromResult(ExecuteOperation(BuildSettingsSnapshot, OperationErrorCode.SettingsReadFailed, "Unable to load saved application settings."));
        }

        public Task<OperationResult<ApplicationInitialStateSnapshot>> GetInitialStateAsync()
        {
            return ExecuteOperationAsync(async () =>
            {
                var settings = BuildSettingsSnapshot();
                FileStorage.SetSessionImagesDirectory(settings.ImagesDirectoryPath);
                await _workflowService.RefreshLocalImageIndexAsync().ConfigureAwait(false);

                var storagePaths = FileStorage.GetStoragePaths();
                var preferredDate = ResolvePreferredDisplayDate(settings);
                var selectedWallpaperStyle = ResolveSelectedWallpaperStyle(settings);
                var validationState = GetApiKeyValidationStateCore();

                return new ApplicationInitialStateSnapshot
                {
                    Settings = settings.Clone(),
                    StoragePaths = storagePaths,
                    ApiKeyValidationState = validationState,
                    PreferredDisplayDate = preferredDate,
                    SelectedWallpaperStyle = selectedWallpaperStyle,
                    LocalImageIndexReady = true,
                };
            }, OperationErrorCode.InitializationFailed, "Unable to build the initial application state.");
        }

        public Task<OperationResult<ApplicationSettingsSnapshot>> SaveSettingsAsync(ApplicationSettingsSnapshot settings)
        {
            return Task.FromResult(ExecuteOperation(() =>
            {
                SaveSettingsCore(settings);
                return BuildSettingsSnapshot();
            }, OperationErrorCode.SettingsWriteFailed, "Unable to save application settings."));
        }

        public Task<OperationResult<string>> UpdateSessionImagesDirectoryAsync(string path)
        {
            return Task.FromResult(ExecuteOperation(() =>
            {
                FileStorage.SetSessionImagesDirectory(path);
                return FileStorage.ImagesDirectory;
            }, OperationErrorCode.StateUpdateFailed, "Unable to update the active images directory for this session."));
        }

        public Task<OperationResult<string>> GetEffectiveImagesDirectoryAsync()
        {
            return Task.FromResult(ExecuteOperation(() => FileStorage.GetStoragePaths().ImagesDirectory, OperationErrorCode.StorageFailed, "Unable to resolve the active images directory."));
        }

        public Task<OperationResult<string>> EnsureEffectiveImagesDirectoryAsync()
        {
            return Task.FromResult(ExecuteOperation(() =>
            {
                return FileStorage.EnsureStorageLayout().ImagesDirectory;
            }, OperationErrorCode.StorageFailed, "Unable to prepare the images directory."));
        }

        public Task<OperationResult<ApplicationStoragePaths>> GetStoragePathsAsync()
        {
            return Task.FromResult(ExecuteOperation(FileStorage.GetStoragePaths, OperationErrorCode.StorageFailed, "Unable to resolve the application storage layout."));
        }

        public Task<OperationResult<ApplicationStoragePaths>> EnsureStorageLayoutAsync()
        {
            return Task.FromResult(ExecuteOperation(FileStorage.EnsureStorageLayout, OperationErrorCode.StorageFailed, "Unable to prepare the application storage layout."));
        }

        public Task<OperationResult<string>> GetUserFriendlyErrorMessageAsync(Exception exception, string fallbackMessage = null)
        {
            return Task.FromResult(ExecuteOperation(
                () =>
                {
                    var message = ApodErrorTranslator.ToUserMessage(exception);
                    return string.IsNullOrWhiteSpace(message)
                        ? (fallbackMessage ?? "Something went wrong while processing the APOD request.")
                        : message;
                },
                OperationErrorCode.Unknown,
                fallbackMessage ?? "Unable to translate the error into a user-friendly message."));
        }

        public Task<OperationResult> LogWarningAsync(string message, Exception exception = null)
        {
            return Task.FromResult(ExecuteOperation(
                () => AppLogger.Warn(message, exception),
                OperationErrorCode.LoggingFailed,
                "Unable to write a warning log entry."));
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

        public Task<OperationResult<ApodWorkflowResult>> LoadDayAsync(DateTime date, bool forceRefresh = false)
        {
            return ExecuteOperationAsync(async () =>
            {
                await EnsureApiKeyValidationIfNeededAsync(date, forceRefresh).ConfigureAwait(false);
                var result = await _workflowService.LoadDayAsync(date, forceRefresh).ConfigureAwait(false);
                _calendarStateService.Clear();
                return EnsureWorkflowResultSucceeded(result, "Unable to load the requested APOD entry.");
            }, OperationErrorCode.WorkflowFailed, "Unable to load the requested APOD entry.");
        }

        public Task<OperationResult<ApodWorkflowResult>> DownloadDayAsync(DateTime date, bool forceRefresh = false)
        {
            return ExecuteOperationAsync(async () =>
            {
                await EnsureApiKeyValidationIfNeededAsync(date, forceRefresh).ConfigureAwait(false);
                var result = await _workflowService.DownloadDayAsync(date, forceRefresh).ConfigureAwait(false);
                _calendarStateService.Clear();
                return EnsureWorkflowResultSucceeded(result, "Unable to download the requested APOD image.");
            }, OperationErrorCode.WorkflowFailed, "Unable to download the requested APOD image.");
        }

        public Task<OperationResult<ApodWorkflowResult>> ApplyDayAsync(DateTime date, WallpaperStyle style, bool forceRefresh = false)
        {
            return ExecuteOperationAsync(async () =>
            {
                await EnsureApiKeyValidationIfNeededAsync(date, forceRefresh).ConfigureAwait(false);
                var result = await _workflowService.ApplyDayAsync(date, style, forceRefresh).ConfigureAwait(false);
                PersistLastAppliedWallpaperImagePath(result.ImagePath);
                _calendarStateService.Clear();
                RaiseWallpaperApplied(result, false);
                return EnsureWorkflowResultSucceeded(result, "Unable to apply the requested APOD image as wallpaper.");
            }, OperationErrorCode.WorkflowFailed, "Unable to apply the requested APOD image as wallpaper.");
        }

        public Task<OperationResult<ApodWorkflowResult>> ApplyLatestPublishedAsync(WallpaperStyle style, bool forceRefresh = false)
        {
            return ExecuteOperationAsync(async () =>
            {
                await EnsureApiKeyValidationAsync().ConfigureAwait(false);
                var result = await _workflowService.ApplyLatestPublishedAsync(style, forceRefresh).ConfigureAwait(false);
                PersistLastAppliedWallpaperImagePath(result.ImagePath);
                _calendarStateService.Clear();
                RaiseWallpaperApplied(result, false);
                return EnsureWorkflowResultSucceeded(result, "Unable to apply the latest available APOD image.");
            }, OperationErrorCode.WorkflowFailed, "Unable to apply the latest available APOD image.");
        }

        public Task<OperationResult<string>> ReapplyCurrentWallpaperStyleAsync(WallpaperStyle style)
        {
            return Task.FromResult(ExecuteOperation(
                () => _workflowService.ReapplyCurrentWallpaperStyle(style),
                OperationErrorCode.WorkflowFailed,
                "Unable to reapply the current wallpaper with the selected style."));
        }

        public Task<OperationResult<string>> GetPostUrlAsync(DateTime date)
        {
            return Task.FromResult(ExecuteOperation(() => _workflowService.GetPostUrl(date), OperationErrorCode.WorkflowFailed, "Unable to resolve the NASA APOD page URL."));
        }

        public Task<OperationResult<ApiKeyValidationState>> GetApiKeyValidationStateAsync()
        {
            return Task.FromResult(ExecuteOperation(GetApiKeyValidationStateCore, OperationErrorCode.SettingsReadFailed, "Unable to read the current API key validation state."));
        }

        public Task<OperationResult<ApodCalendarMonthState>> GetCalendarMonthStateAsync(DateTime month, bool refreshMissingDates, MonthRefreshMode refreshMode)
        {
            return ExecuteOperationAsync(
                () => Task.Run(() => _calendarStateService.GetMonthState(month, refreshMissingDates, refreshMode)),
                OperationErrorCode.WorkflowFailed,
                "Unable to build calendar month state.");
        }

        public Task<OperationResult<IReadOnlyList<DateTime>>> GetFavoriteDatesAsync()
        {
            return ExecuteOperationAsync(async () =>
            {
                await _workflowService.RefreshLocalImageIndexAsync().ConfigureAwait(false);
                var dates = _favoriteStore
                    .GetDates()
                    .Where(date => _workflowService.HasUsableLocalImage(date))
                    .OrderByDescending(date => date)
                    .ToList();

                return (IReadOnlyList<DateTime>)dates;
            }, OperationErrorCode.StorageFailed, "Unable to load favorite APOD dates.");
        }

        public Task<OperationResult<IReadOnlyList<FavoriteApodItem>>> GetFavoriteApodsAsync()
        {
            return ExecuteOperationAsync(async () =>
            {
                await _workflowService.RefreshLocalImageIndexAsync().ConfigureAwait(false);
                var items = _favoriteStore
                    .GetDates()
                    .Select(date => new FavoriteApodItem
                    {
                        Date = date.Date,
                        ImagePath = _workflowService.GetLocalImagePath(date),
                        Title = "APOD " + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    })
                    .Where(item => !string.IsNullOrWhiteSpace(item.ImagePath))
                    .OrderByDescending(item => item.Date)
                    .ToList();

                return (IReadOnlyList<FavoriteApodItem>)items;
            }, OperationErrorCode.StorageFailed, "Unable to load favorite APOD images.");
        }

        public Task<OperationResult<bool>> IsFavoriteAsync(DateTime date)
        {
            return ExecuteOperationAsync(async () =>
            {
                await _workflowService.RefreshLocalImageIndexAsync().ConfigureAwait(false);
                return _favoriteStore.IsFavorite(date.Date) && _workflowService.HasUsableLocalImage(date.Date);
            }, OperationErrorCode.StorageFailed, "Unable to read favorite APOD state.");
        }

        public Task<OperationResult<bool>> SetFavoriteAsync(DateTime date, bool isFavorite)
        {
            return ExecuteOperationAsync(async () =>
            {
                await _workflowService.RefreshLocalImageIndexAsync().ConfigureAwait(false);
                if (isFavorite && !_workflowService.HasUsableLocalImage(date.Date))
                    throw new InvalidOperationException("Only locally downloaded APOD images can be added to favorites.");

                return _favoriteStore.SetFavorite(date.Date, isFavorite);
            }, OperationErrorCode.StorageFailed, isFavorite ? "Unable to add APOD image to favorites." : "Unable to remove APOD image from favorites.");
        }

        public Task<OperationResult<ApodPageAvailabilityProbeResult>> ProbeApodPageAvailabilityAsync(DateTime date)
        {
            return ExecuteOperationAsync(
                () => _pageAvailabilityProbe.ProbeAsync(date.Date, TimeSpan.FromSeconds(2)),
                OperationErrorCode.WorkflowFailed,
                "Unable to probe the NASA APOD page availability.");
        }

        public Task<OperationResult<UpdateCheckResult>> CheckForUpdatesAsync(string currentVersion, bool forceCheck, bool automatic)
        {
            return ExecuteOperationAsync(async () =>
            {
                var settings = BuildSettingsSnapshot();
                if (automatic)
                {
                    if (!settings.AutoCheckUpdatesEnabled)
                        return CreateSkippedUpdateCheckResult(currentVersion, "Automatic update checks are disabled.");

                    var lastCheckUtc = ParseDateTime(settings.LastUpdateCheckUtc);
                    if (!forceCheck && lastCheckUtc.HasValue && DateTime.UtcNow - lastCheckUtc.Value < TimeSpan.FromDays(1))
                        return CreateSkippedUpdateCheckResult(currentVersion, "Update check was already completed today.");
                }

                var result = await _updateCheckService.CheckLatestReleaseAsync(currentVersion).ConfigureAwait(false);
                PersistLastUpdateCheckUtc(result.CheckedAtUtc);
                return result;
            }, OperationErrorCode.WorkflowFailed, "Unable to check GitHub releases for updates.", retryable: true);
        }

        public Task<OperationResult> RefreshLocalImageIndexAsync()
        {
            return ExecuteOperationAsync(async () =>
            {
                await _workflowService.RefreshLocalImageIndexAsync().ConfigureAwait(false);
                _calendarStateService.Clear();
            }, OperationErrorCode.StorageFailed, "Unable to refresh the local image index.");
        }

        public Task<OperationResult<bool>> ShouldApplyOnTrayDoubleClickAsync()
        {
            return Task.FromResult(ExecuteOperation(() => BuildSettingsSnapshot().TrayDoubleClickAction, OperationErrorCode.SettingsReadFailed, "Unable to read tray double-click behavior."));
        }

        public Task<OperationResult<DateTime>> GetPreferredDisplayDateAsync()
        {
            return Task.FromResult(ExecuteOperation(() => ResolvePreferredDisplayDate(BuildSettingsSnapshot()), OperationErrorCode.SettingsReadFailed, "Unable to resolve the preferred display date."));
        }

        public Task<OperationResult<WallpaperStyle>> GetSelectedWallpaperStyleAsync()
        {
            return Task.FromResult(ExecuteOperation(() => ResolveSelectedWallpaperStyle(BuildSettingsSnapshot()), OperationErrorCode.SettingsReadFailed, "Unable to read the selected wallpaper style."));
        }

        public Task<OperationResult> ShutdownAsync()
        {
            return Task.FromResult(ExecuteOperation(
                ShutdownCore,
                OperationErrorCode.ShutdownFailed,
                "Unable to shut down the application controller cleanly."));
        }

        void IDisposable.Dispose()
        {
            ShutdownCore();
        }

        private ApplicationSettingsSnapshot BuildSettingsSnapshot()
        {
            var settings = _settingsStore.Load() ?? new ApplicationSettingsSnapshot();
            settings.NasaApiKey = GetStoredApiKeyForDisplay();
            settings.NasaApiKeyValidationState = NormalizeValidationState(settings.NasaApiKeyValidationState);
            settings.Language = ApplicationSettingsSnapshot.NormalizeLanguage(settings.Language);
            settings.TranslationTargetLanguage = ApplicationSettingsSnapshot.NormalizeTranslationTargetLanguage(settings.TranslationTargetLanguage);
            settings.AutoWallpaperSource = ApplicationSettingsSnapshot.NormalizeAutoWallpaperSource(settings.AutoWallpaperSource);
            settings.ImagesDirectoryPath = Normalize(settings.ImagesDirectoryPath);
            settings.LastAutoRefreshRunDate = Normalize(settings.LastAutoRefreshRunDate);
            settings.LastAutoRefreshAppliedDate = Normalize(settings.LastAutoRefreshAppliedDate);
            settings.LastFavoriteWallpaperDate = Normalize(settings.LastFavoriteWallpaperDate);
            settings.LastAppliedWallpaperImagePath = Normalize(settings.LastAppliedWallpaperImagePath);
            settings.LastUpdateCheckUtc = Normalize(settings.LastUpdateCheckUtc);
            return settings;
        }

        private void SaveSettingsCore(ApplicationSettingsSnapshot settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var persistedSettings = _settingsStore.Load() ?? new ApplicationSettingsSnapshot();
            var previousAutoRefreshEnabled = persistedSettings.AutoRefreshEnabled;
            var previousApiKey = Normalize(GetStoredApiKeyForDisplay());
            var normalizedApiKey = NormalizeApiKeyForDisplay(settings.NasaApiKey);
            var apiKeyChanged = !string.Equals(previousApiKey, normalizedApiKey, StringComparison.Ordinal);
            var effectiveValidationState = apiKeyChanged
                ? ApiKeyValidationState.Unknown.ToString()
                : (string.IsNullOrWhiteSpace(settings.NasaApiKeyValidationState)
                    ? NormalizeValidationState(persistedSettings.NasaApiKeyValidationState)
                    : NormalizeValidationState(settings.NasaApiKeyValidationState));

            SaveStoredApiKey(settings.NasaApiKey);
            _settingsStore.Save(new ApplicationSettingsSnapshot
            {
                TrayDoubleClickAction = settings.TrayDoubleClickAction,
                WallpaperStyleIndex = settings.WallpaperStyleIndex,
                AutoRefreshEnabled = settings.AutoRefreshEnabled,
                AutoWallpaperSource = ApplicationSettingsSnapshot.NormalizeAutoWallpaperSource(settings.AutoWallpaperSource),
                StartWithWindows = settings.StartWithWindows,
                MinimizeToTrayOnClose = settings.MinimizeToTrayOnClose,
                Language = ApplicationSettingsSnapshot.NormalizeLanguage(settings.Language),
                TranslationTargetLanguage = ApplicationSettingsSnapshot.NormalizeTranslationTargetLanguage(settings.TranslationTargetLanguage),
                NasaApiKeyValidationState = effectiveValidationState,
                ImagesDirectoryPath = Normalize(settings.ImagesDirectoryPath),
                LastAutoRefreshRunDate = !previousAutoRefreshEnabled && settings.AutoRefreshEnabled
                    ? string.Empty
                    : Normalize(settings.LastAutoRefreshRunDate),
                LastAutoRefreshAppliedDate = !previousAutoRefreshEnabled && settings.AutoRefreshEnabled
                    ? string.Empty
                    : Normalize(settings.LastAutoRefreshAppliedDate),
                LastFavoriteWallpaperDate = Normalize(settings.LastFavoriteWallpaperDate),
                LastAppliedWallpaperImagePath = Normalize(settings.LastAppliedWallpaperImagePath),
                AutoCheckUpdatesEnabled = settings.AutoCheckUpdatesEnabled,
                SuppressAutomaticUpdateReminder = settings.SuppressAutomaticUpdateReminder,
                LastUpdateCheckUtc = Normalize(settings.LastUpdateCheckUtc),
            });

            if (apiKeyChanged)
                ResetApiKeyValidationRuntimeState();

            ApplyRuntimeSettings();
            _calendarStateService.Clear();
            if (previousAutoRefreshEnabled != settings.AutoRefreshEnabled || apiKeyChanged)
                ConfigureScheduler(BuildSettingsSnapshot());
            _startupRegistrationService.SetStartWithWindows(settings.StartWithWindows);
        }

        private ApiKeyValidationState GetApiKeyValidationStateCore()
        {
            return ParseValidationState((_settingsStore.Load() ?? new ApplicationSettingsSnapshot()).NasaApiKeyValidationState);
        }

        private void ApplyRuntimeSettings()
        {
            var settings = BuildSettingsSnapshot();
            AppRuntimeSettings.Configure(settings.NasaApiKey, settings.ImagesDirectoryPath, ParseValidationState(settings.NasaApiKeyValidationState));
            FileStorage.SetSessionImagesDirectory(settings.ImagesDirectoryPath);
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

        private static OperationResult ExecuteOperation(Action operation, OperationErrorCode errorCode, string failureMessage, bool retryable = false)
        {
            try
            {
                operation();
                return OperationResult.Success();
            }
            catch (Exception ex)
            {
                AppLogger.Warn(failureMessage, ex);
                return OperationResult.Failure(CreateOperationError(errorCode, failureMessage, ex, retryable));
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
                if (ApplicationSettingsSnapshot.NormalizeAutoWallpaperSource(settings.AutoWallpaperSource) == AutoWallpaperSource.Favorites)
                {
                    RunScheduledFavoriteWallpaperUpdate(settings, localToday, lastRunDate);
                    return;
                }

                // Day-level short circuit: once scheduler has already finished successfully for local "today",
                // do not burn extra API/HTML checks until the next day.
                if (ShouldSkipSchedulerForToday(lastRunDate, lastAppliedDate, localToday) &&
                    IsLastAppliedWallpaperCurrentlyActive(settings))
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
                    lastAppliedDate.Value == latestAvailableDate &&
                    IsLastAppliedWallpaperCurrentlyActive(settings))
                {
                    AppLogger.Info("Scheduler skipped because the latest available APOD was already applied today.");
                    return;
                }

                if (lastAppliedDate.HasValue &&
                    lastAppliedDate.Value == latestAvailableDate &&
                    IsLastAppliedWallpaperCurrentlyActive(settings))
                {
                    if (latestPublicationIsForToday)
                    {
                        settings.LastAutoRefreshAppliedDate = latestAvailableDate.ToString("yyyy-MM-dd");
                        settings.LastAutoRefreshRunDate = localToday.ToString("yyyy-MM-dd");
                        _settingsStore.Save(settings);
                        _calendarStateService.Clear();
                        RaiseWallpaperApplied(CreateAutomaticCheckResult(localToday, latestAvailableDate, latestPublishedDate, settings.LastAppliedWallpaperImagePath), true);
                    }

                    AppLogger.Info("Scheduler checked APOD and found the same latest available image already applied: " + latestAvailableDate.ToString("yyyy-MM-dd") + ".");
                    return;
                }

                var result = _workflowService.ApplyDay(latestAvailableDate, (WallpaperStyle)settings.WallpaperStyleIndex, false);
                if (!result.IsSuccess)
                    return;

                var resolvedDate = result.ResolvedDate.HasValue
                    ? result.ResolvedDate.Value.Date
                    : latestAvailableDate;
                result.LatestPublishedDate = latestPublishedDate;

                _calendarStateService.Clear();
                PersistLastAppliedWallpaperImagePath(result.ImagePath);

                settings.LastAutoRefreshAppliedDate = resolvedDate.ToString("yyyy-MM-dd");
                settings.LastAutoRefreshRunDate = latestPublicationIsForToday
                    ? localToday.ToString("yyyy-MM-dd")
                    : Normalize(settings.LastAutoRefreshRunDate);
                settings.LastAppliedWallpaperImagePath = Normalize(result.ImagePath);
                _settingsStore.Save(settings);
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

        private void RunScheduledFavoriteWallpaperUpdate(ApplicationSettingsSnapshot settings, DateTime localToday, DateTime? lastRunDate)
        {
            if (lastRunDate.HasValue &&
                lastRunDate.Value == localToday &&
                IsLastAppliedWallpaperCurrentlyActive(settings))
            {
                AppLogger.Info("Scheduler skipped favorite rotation because it already completed today.");
                return;
            }

            _workflowService.RefreshLocalImageIndexAsync().GetAwaiter().GetResult();
            var favoriteDates = _favoriteStore
                .GetDates()
                .Where(date => _workflowService.HasUsableLocalImage(date))
                .OrderBy(date => date)
                .ToList();

            var selectedDate = SelectFavoriteRotationDate(favoriteDates, ParseDate(settings.LastFavoriteWallpaperDate));
            if (!selectedDate.HasValue)
            {
                AppLogger.Info("Scheduler skipped favorite rotation because no local favorite images are available.");
                return;
            }

            var result = _workflowService.ApplyDay(selectedDate.Value, (WallpaperStyle)settings.WallpaperStyleIndex, false);
            if (!result.IsSuccess)
                return;

            var resolvedDate = result.ResolvedDate.HasValue
                ? result.ResolvedDate.Value.Date
                : selectedDate.Value.Date;

            _calendarStateService.Clear();
            PersistLastAppliedWallpaperImagePath(result.ImagePath);

            settings.LastFavoriteWallpaperDate = resolvedDate.ToString("yyyy-MM-dd");
            settings.LastAutoRefreshAppliedDate = resolvedDate.ToString("yyyy-MM-dd");
            settings.LastAutoRefreshRunDate = localToday.ToString("yyyy-MM-dd");
            settings.LastAppliedWallpaperImagePath = Normalize(result.ImagePath);
            _settingsStore.Save(settings);
            RaiseWallpaperApplied(result, true);
        }

        internal static DateTime? SelectFavoriteRotationDate(IReadOnlyList<DateTime> favoriteDates, DateTime? lastFavoriteDate)
        {
            if (favoriteDates == null || favoriteDates.Count == 0)
                return null;

            var candidates = favoriteDates
                .Select(date => date.Date)
                .Distinct()
                .OrderBy(date => date)
                .ToList();
            if (candidates.Count == 0)
                return null;

            if (lastFavoriteDate.HasValue && candidates.Count > 1)
            {
                candidates = candidates
                    .Where(date => date != lastFavoriteDate.Value.Date)
                    .ToList();
            }

            return candidates[new Random().Next(candidates.Count)];
        }

        internal static TimeSpan ResolveSchedulerPollingInterval(ApplicationSettingsSnapshot settings)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.NasaApiKey) || string.Equals(settings.NasaApiKey.Trim(), DemoApiKey, StringComparison.OrdinalIgnoreCase))
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

        private static DateTime ResolvePreferredDisplayDate(ApplicationSettingsSnapshot settings)
        {
            var lastAppliedDate = ParseDate(settings != null ? settings.LastAutoRefreshAppliedDate : null);
            if (lastAppliedDate.HasValue && lastAppliedDate.Value <= DateTime.Today)
                return lastAppliedDate.Value;

            return DateTime.Today;
        }

        private static WallpaperStyle ResolveSelectedWallpaperStyle(ApplicationSettingsSnapshot settings)
        {
            if (settings == null)
                return WallpaperStyle.Smart;

            return Enum.IsDefined(typeof(WallpaperStyle), settings.WallpaperStyleIndex)
                ? (WallpaperStyle)settings.WallpaperStyleIndex
                : WallpaperStyle.Smart;
        }

        private static DateTime? ParseDate(string value)
        {
            DateTime parsedDate;
            return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate)
                ? parsedDate.Date
                : (DateTime?)null;
        }

        private static DateTime? ParseDateTime(string value)
        {
            DateTime parsed;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
                return parsed.ToUniversalTime();

            return null;
        }

        private static UpdateCheckResult CreateSkippedUpdateCheckResult(string currentVersion, string message)
        {
            return new UpdateCheckResult
            {
                Status = UpdateCheckStatus.Skipped,
                CurrentVersion = UpdateCheckService.NormalizeVersionText(currentVersion),
                CheckedAtUtc = DateTime.UtcNow,
                Message = message,
            };
        }

        private void PersistLastUpdateCheckUtc(DateTime checkedAtUtc)
        {
            var snapshot = BuildSettingsSnapshot();
            snapshot.LastUpdateCheckUtc = checkedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            SaveSettingsCore(snapshot);
        }

        private static ApiKeyValidationState ParseValidationState(string value)
        {
            ApiKeyValidationState parsedState;
            return Enum.TryParse(value, true, out parsedState)
                ? parsedState
                : ApiKeyValidationState.Unknown;
        }

        internal static ApodWorkflowResult EnsureWorkflowResultSucceeded(ApodWorkflowResult result, string fallbackMessage)
        {
            if (result == null)
                throw new InvalidOperationException(fallbackMessage);

            if (result.Status != ApodWorkflowStatus.Failed)
                return result;

            var message = string.IsNullOrWhiteSpace(result.Message)
                ? fallbackMessage
                : result.Message;
            throw new InvalidOperationException(message);
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
            var settings = _settingsStore.Load() ?? new ApplicationSettingsSnapshot();
            settings.NasaApiKeyValidationState = validationState.ToString();
            _settingsStore.Save(settings);
            ApplyRuntimeSettings();
        }

        private string GetStoredApiKeyForDisplay()
        {
            var storedApiKey = Normalize(_secretStore.GetNasaApiKey());
            return string.IsNullOrWhiteSpace(storedApiKey)
                ? DemoApiKey
                : storedApiKey;
        }

        private void SaveStoredApiKey(string apiKey)
        {
            var normalizedApiKey = Normalize(apiKey);
            if (string.IsNullOrWhiteSpace(normalizedApiKey) ||
                string.Equals(normalizedApiKey, DemoApiKey, StringComparison.OrdinalIgnoreCase))
            {
                _secretStore.DeleteNasaApiKey();
                return;
            }

            _secretStore.SaveNasaApiKey(normalizedApiKey);
        }

        private static string NormalizeApiKeyForDisplay(string apiKey)
        {
            var normalizedApiKey = Normalize(apiKey);
            return string.IsNullOrWhiteSpace(normalizedApiKey)
                ? DemoApiKey
                : normalizedApiKey;
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

        private static ApodWorkflowResult CreateAutomaticCheckResult(DateTime requestedDate, DateTime resolvedDate, DateTime latestPublishedDate, string imagePath)
        {
            return new ApodWorkflowResult
            {
                Status = ApodWorkflowStatus.Success,
                RequestedDate = requestedDate.Date,
                ResolvedDate = resolvedDate.Date,
                LatestPublishedDate = latestPublishedDate.Date,
                ImagePath = imagePath,
                PreviewLocation = imagePath,
                IsLocalFile = LocalImageValidator.IsUsableImageFile(imagePath),
                Source = ApodDataSource.LocalFile,
            };
        }

        private void PersistLastAppliedWallpaperImagePath(string imagePath)
        {
            if (!LocalImageValidator.IsUsableImageFile(imagePath))
                return;

            var settings = _settingsStore.Load() ?? new ApplicationSettingsSnapshot();
            settings.LastAppliedWallpaperImagePath = Normalize(imagePath);
            _settingsStore.Save(settings);
        }

        private bool IsLastAppliedWallpaperCurrentlyActive(ApplicationSettingsSnapshot settings)
        {
            if (settings == null || !LocalImageValidator.IsUsableImageFile(settings.LastAppliedWallpaperImagePath))
                return false;

            var currentSourcePath = _workflowService.ResolveCurrentWallpaperSourcePath();
            return string.Equals(
                Normalize(currentSourcePath),
                Normalize(settings.LastAppliedWallpaperImagePath),
                StringComparison.OrdinalIgnoreCase);
        }

        private void UnsubscribeWallpaperApplied(EventHandler<WallpaperAppliedEventArgs> handler)
        {
            if (handler == null)
                return;

            WallpaperApplied -= handler;
        }

        internal static bool ShouldSkipSchedulerForToday(DateTime? lastRunDate, DateTime? lastAppliedDate, DateTime localToday)
        {
            return lastRunDate.HasValue &&
                   lastRunDate.Value == localToday.Date &&
                   lastAppliedDate.HasValue &&
                   lastAppliedDate.Value >= localToday.Date;
        }

        private static OperationError CreateOperationError(OperationErrorCode code, string message, Exception exception, bool retryable)
        {
            return new OperationError(code, message, retryable, exception != null ? exception.GetType().Name + ": " + exception.Message : null);
        }

        private sealed class WallpaperAppliedSubscription : IEventSubscription
        {
            private readonly ApplicationController _owner;
            private EventHandler<WallpaperAppliedEventArgs> _handler;

            public WallpaperAppliedSubscription(ApplicationController owner, EventHandler<WallpaperAppliedEventArgs> handler)
            {
                _owner = owner;
                _handler = handler;
            }

            public void Dispose()
            {
                var handler = Interlocked.Exchange(ref _handler, null);
                if (handler != null)
                    _owner.UnsubscribeWallpaperApplied(handler);
            }
        }
    }
}
