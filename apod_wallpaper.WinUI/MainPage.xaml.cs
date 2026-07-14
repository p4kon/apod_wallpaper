using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;

namespace apod_wallpaper.WinUI;

public sealed partial class MainPage : Page
{
    private static readonly CultureInfo UiDateCulture = CultureInfo.InvariantCulture;

    private sealed class MonthCacheEntry
    {
        public required apod_wallpaper.ApodCalendarMonthState State { get; init; }
        public long LastAccessStamp { get; set; }
    }

    private sealed class CalendarDayVisual
    {
        public required DateTime Date { get; init; }
        public required Button Button { get; init; }
        public required TextBlock DayNumberText { get; init; }
        public required TextBlock StatusText { get; init; }
        public bool IsLoading { get; set; }
        public apod_wallpaper.ApodCalendarDayState? CurrentDayState { get; set; }
        public DateTime LatestPublishedDate { get; set; }
        public string? LastVisualSignature { get; set; }
    }

    private sealed class TranslationTargetLanguageOption
    {
        public TranslationTargetLanguageOption(string code, string nameKey)
        {
            Code = code;
            NameKey = nameKey;
        }

        public string Code { get; }
        public string NameKey { get; }
    }

    private static readonly SolidColorBrush CalendarGreenBrush = new(ColorHelper.FromArgb(0xFF, 0x3D, 0x8C, 0x63));
    private static readonly SolidColorBrush CalendarBlueBrush = new(ColorHelper.FromArgb(0xFF, 0x2F, 0x79, 0xD9));
    private static readonly SolidColorBrush CalendarRedBrush = new(ColorHelper.FromArgb(0xFF, 0xC4, 0x5A, 0x5A));
    private static readonly SolidColorBrush CalendarDefaultForegroundBrush = new(Colors.White);
    private static readonly SolidColorBrush CalendarLightUnknownBrush = new(ColorHelper.FromArgb(0xFF, 0xCB, 0xD5, 0xE1));
    private static readonly SolidColorBrush CalendarLightFutureBrush = new(ColorHelper.FromArgb(0xFF, 0xF8, 0xFA, 0xFC));
    private static readonly SolidColorBrush CalendarLightMutedForegroundBrush = new(ColorHelper.FromArgb(0xFF, 0x64, 0x74, 0x8B));
    private static readonly SolidColorBrush CalendarLightDefaultForegroundBrush = new(ColorHelper.FromArgb(0xFF, 0x1E, 0x29, 0x3B));
    private static readonly SolidColorBrush CalendarDarkUnknownBrush = new(ColorHelper.FromArgb(0xFF, 0x5F, 0x5F, 0x5F));
    private static readonly SolidColorBrush CalendarDarkFutureBrush = new(ColorHelper.FromArgb(0xFF, 0x31, 0x31, 0x31));
    private static readonly SolidColorBrush CalendarDarkFutureForegroundBrush = new(ColorHelper.FromArgb(0xFF, 0xAA, 0xAA, 0xAA));
    private static readonly SolidColorBrush CalendarSelectedLightBorderBrush = new(ColorHelper.FromArgb(0xFF, 0x25, 0x63, 0xEB));
    private static readonly SolidColorBrush CalendarSelectedDarkBorderBrush = new(ColorHelper.FromArgb(0xFF, 0xF1, 0xF5, 0xF9));
    private static readonly apod_wallpaper.WallpaperStyle[] WallpaperStyleDisplayOrder =
    {
        apod_wallpaper.WallpaperStyle.Smart,
        apod_wallpaper.WallpaperStyle.Fill,
        apod_wallpaper.WallpaperStyle.Fit,
        apod_wallpaper.WallpaperStyle.Stretch,
        apod_wallpaper.WallpaperStyle.Tile,
        apod_wallpaper.WallpaperStyle.Center,
        apod_wallpaper.WallpaperStyle.Span,
    };
    private static readonly TranslationTargetLanguageOption[] TranslationTargetLanguages =
    {
        new(apod_wallpaper.TranslationTargetLanguage.Russian, "Russian"),
        new(apod_wallpaper.TranslationTargetLanguage.Spanish, "Spanish"),
        new(apod_wallpaper.TranslationTargetLanguage.German, "German"),
        new(apod_wallpaper.TranslationTargetLanguage.French, "French"),
        new(apod_wallpaper.TranslationTargetLanguage.Italian, "Italian"),
        new(apod_wallpaper.TranslationTargetLanguage.Portuguese, "Portuguese"),
        new(apod_wallpaper.TranslationTargetLanguage.Japanese, "Japanese"),
    };

    private BackendHost? _backendHost;
    private apod_wallpaper.OperationResult<apod_wallpaper.ApplicationSettingsSnapshot>? _initialization;
    private TraySpikeStatus? _trayStatus;
    private apod_wallpaper.ApplicationInitialStateSnapshot? _initialStateSnapshot;
    private apod_wallpaper.ApodCalendarMonthState? _currentMonthState;
    private apod_wallpaper.ApplicationSettingsSnapshot? _currentSettingsSnapshot;
    private apod_wallpaper.ApodWorkflowResult? _lastPreviewWorkflow;
    private int _previewRequestVersion;
    private int _monthRequestVersion;
    private readonly HashSet<DateTime> _warmedMonths = new();
    private readonly Dictionary<DateTime, MonthCacheEntry> _hotMonthCache = new();
    private readonly HashSet<DateTime> _monthsInFlight = new();
    private readonly Dictionary<DateTime, CalendarDayVisual> _calendarDayVisuals = new();
    private DateTime _selectedDate = DateTime.Today;
    private DateTime _visibleMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime? _renderedCalendarMonth;
    private readonly Stopwatch _previewImageStopwatch = new();
    private long _lastPreviewBackendElapsedMs;
    private long _lastMonthCachedElapsedMs;
    private string? _pendingPreviewLocation;
    private string? _previewCacheDirectory;
    private long _monthCacheAccessStamp;
    private const int WarmupVisualStepDelayMs = 16;
    private const int PinnedMonthWindowRadius = 1;
    private const int RecentMonthHistoryLimit = 3;
    private const int HotMonthCacheLimit = (PinnedMonthWindowRadius * 2) + 1 + RecentMonthHistoryLimit;
    private static readonly HttpClient PreviewAssetHttpClient = CreatePreviewAssetHttpClient();
    private readonly Dictionary<string, Task<string?>> _previewAssetTasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _previewAssetSyncRoot = new();
    private bool _isApplyingWallpaperAction;
    private bool _suppressWallpaperStyleSelectionChanged;
    private bool _suppressAutoRefreshToggleChanged;
    private apod_wallpaper.IEventSubscription? _wallpaperAppliedSubscription;
    private bool _wallpaperAppliedSubscriptionRequested;
    private int _previewPixelWidth;
    private int _previewPixelHeight;
    private static readonly SolidColorBrush AutoRefreshEnabledBrush = new(ColorHelper.FromArgb(0xFF, 0x1F, 0xB5, 0x7A));
    private static readonly SolidColorBrush AutoRefreshDisabledBrush = new(ColorHelper.FromArgb(0xFF, 0xC4, 0x5A, 0x5A));
    private static readonly SolidColorBrush AutoRefreshForegroundBrush = new(Colors.White);
    private const int GoogleTranslateMaxUrlLength = 7800;
    private static readonly TimeSpan TodayAvailabilityProbeThrottle = TimeSpan.FromMinutes(5);
    private string _originalExplanationText = string.Empty;
    private string _displayedExplanationText = string.Empty;
    private bool _isExplanationTranslated;
    private DateTime? _transientAvailableApodDate;
    private DateTime? _lastTodayAvailabilityProbeDate;
    private DateTime _lastTodayAvailabilityProbeUtc = DateTime.MinValue;
    private Task? _todayAvailabilityProbeTask;

    public MainPage()
    {
        InitializeComponent();
        LocalizationHelper.ApplyTo(this);
        NavigationCacheMode = NavigationCacheMode.Required;
        WallpaperStyleComboBox.ItemsSource = WallpaperStyleDisplayOrder;
        EnsureCalendarGridDefinitions();
        VisibleMonthText.Text = FormatVisibleMonth(_visibleMonth);
        RebuildTranslationTargetLanguageFlyout();
        UpdateTranslationTargetLanguageSelector();
        UpdateExplanationActionState();
        RefreshSelectedDateText();
        EnsureCalendarMonthBuilt(_visibleMonth);
        UpdateActionAvailability();
        AppStrings.LanguageChanged += AppStrings_LanguageChanged;
        Loaded += MainPage_Loaded;
        ActualThemeChanged += MainPage_ActualThemeChanged;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        var arguments = e.Parameter as MainPageArguments;
        if (arguments == null)
        {
            SetErrorState(AppStrings.Get("Main page did not receive backend composition root arguments."));
            return;
        }

        _backendHost = arguments.BackendHost;
        _initialization = arguments.Initialization;
        _trayStatus = arguments.TrayStatus;
        _trayStatus.Changed += TrayStatus_Changed;
        RefreshTrayStatus();

        if (_initialStateSnapshot != null)
        {
            await RefreshSettingsSnapshotPreservingCalendarAsync();
            QueueTodayAvailabilityProbe();
            return;
        }

        if (_initialization == null || !_initialization.Succeeded)
        {
            SetErrorState(AppStrings.GetBackendMessageOrDefault(_initialization?.Error?.Message, "Backend initialization failed before the main page loaded."));
            return;
        }

        await EnsureWallpaperAppliedSubscriptionAsync();
        await RefreshStateAsync();
        QueueTodayAvailabilityProbe();
    }

    private void MainPage_ActualThemeChanged(FrameworkElement sender, object args)
    {
        UpdateCalendarSelectionOnly();
        UpdateAutoRefreshToggleVisual();
    }

    private void AppStrings_LanguageChanged(object? sender, EventArgs e)
    {
        RefreshLocalizedText();
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshLocalizedText();
        QueueTodayAvailabilityProbe();
    }

    internal void NotifyHostReturnedToCalendar()
    {
        QueueTodayAvailabilityProbe();
    }

    private void RefreshLocalizedText()
    {
        LocalizationHelper.ApplyTo(this);
        VisibleMonthText.Text = FormatVisibleMonth(_visibleMonth);
        RefreshSelectedDateText();
        UpdateAutoRefreshToggleVisual();
        RebuildTranslationTargetLanguageFlyout();
        UpdateTranslationTargetLanguageSelector();
        UpdateExplanationActionState();
        UpdateActionAvailability();
        UpdateCalendarSelectionOnly();
        RefreshTrayStatus();
    }

    private async void ReloadPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadPreviewForSelectedDateAsync();
    }

    private async void DownloadOnlyButton_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteWallpaperActionAsync(
            AppStrings.Get("Downloading image"),
            AppStrings.Format("Downloading APOD image for {0}.", _selectedDate.ToString("yyyy-MM-dd")),
            () => _backendHost!.Backend.DownloadDayAsync(_selectedDate));
    }

    private async void DownloadAndApplyButton_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteWallpaperActionAsync(
            AppStrings.Get("Applying wallpaper"),
            AppStrings.Format("Downloading and applying APOD for {0}.", _selectedDate.ToString("yyyy-MM-dd")),
            () => _backendHost!.Backend.ApplyDayAsync(_selectedDate, GetSelectedWallpaperStyle()),
            disableAutoRefreshOnSuccess: true);
    }

    private async void AutoRefreshToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        await SaveAutoRefreshFromMainAsync(true);
    }

    private async void AutoRefreshToggleButton_Unchecked(object sender, RoutedEventArgs e)
    {
        await SaveAutoRefreshFromMainAsync(false);
    }

    private async void ApplyLatestButton_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteWallpaperActionAsync(
            AppStrings.Get("Applying latest APOD"),
            AppStrings.Get("Requesting the latest available APOD and applying it as wallpaper."),
            () => _backendHost!.Backend.ApplyLatestPublishedAsync(GetSelectedWallpaperStyle()));
    }

    private async void OpenNasaPageButton_Click(object sender, RoutedEventArgs e)
    {
        if (_backendHost == null)
            return;

        var postUrlResult = await _backendHost.Backend.GetPostUrlAsync(_selectedDate);
        if (!postUrlResult.Succeeded || string.IsNullOrWhiteSpace(postUrlResult.Value))
        {
            PreviewStatusBar.Severity = InfoBarSeverity.Error;
            PreviewStatusBar.Title = AppStrings.Get("Unable to open NASA page");
            PreviewStatusBar.Message = AppStrings.GetBackendMessageOrDefault(postUrlResult.Error?.Message, "The backend did not return a valid APOD page URL.");
            return;
        }

        await Launcher.LaunchUriAsync(new Uri(postUrlResult.Value));
    }

    private void CopyExplanationButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_displayedExplanationText))
            return;

        if (TryCopyTextToClipboard(_displayedExplanationText))
        {
            ActionStatusBar.Severity = InfoBarSeverity.Success;
            ActionStatusBar.Title = AppStrings.Get("Copied");
            ActionStatusBar.Message = string.Empty;
        }
    }

    private async void TranslateExplanationButton_Click(object sender, RoutedEventArgs e)
    {
        var targetLanguage = GetSelectedTranslationTargetLanguage();
        if (string.IsNullOrEmpty(targetLanguage) || string.IsNullOrWhiteSpace(_originalExplanationText))
            return;

        try
        {
            var urlWithText = apod_wallpaper.TranslationTargetLanguage.BuildGoogleTranslateUrl(
                targetLanguage,
                _originalExplanationText,
                includeText: true);
            var useLongUrlFallback = urlWithText.Length > GoogleTranslateMaxUrlLength;
            var url = useLongUrlFallback
                ? apod_wallpaper.TranslationTargetLanguage.BuildGoogleTranslateUrl(targetLanguage, string.Empty, includeText: false)
                : urlWithText;

            if (useLongUrlFallback)
            {
                if (!TryCopyTextToClipboard(_originalExplanationText))
                    return;

                ActionStatusBar.Severity = InfoBarSeverity.Informational;
                ActionStatusBar.Title = AppStrings.Get("Text copied. Paste it into Google Translate.");
                ActionStatusBar.Message = string.Empty;
            }

            var launched = await Launcher.LaunchUriAsync(new Uri(url));
            if (!launched)
                ShowTranslateOpenFailed();
        }
        catch
        {
            ShowTranslateOpenFailed();
        }
    }

    private async void TranslationTargetLanguageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem item || item.Tag is not string language || _backendHost == null || _currentSettingsSnapshot == null)
            return;

        var normalizedLanguage = apod_wallpaper.ApplicationSettingsSnapshot.NormalizeTranslationTargetLanguage(language);
        if (string.Equals(GetSelectedTranslationTargetLanguage(), normalizedLanguage, StringComparison.Ordinal))
            return;

        var updatedSnapshot = await GetFreshSettingsSnapshotAsync();
        updatedSnapshot.TranslationTargetLanguage = normalizedLanguage;

        var saveResult = await _backendHost.Backend.SaveSettingsAsync(updatedSnapshot);
        if (!saveResult.Succeeded || saveResult.Value == null)
        {
            ActionStatusBar.Severity = InfoBarSeverity.Error;
            ActionStatusBar.Title = AppStrings.Get("Settings were not saved");
            ActionStatusBar.Message = AppStrings.GetBackendMessageOrDefault(saveResult.Error?.Message, "Unknown backend error while saving settings.");
            return;
        }

        ApplySavedSettingsSnapshot(saveResult.Value);
        ActionStatusBar.Severity = InfoBarSeverity.Success;
        ActionStatusBar.Title = AppStrings.Get("Settings saved");
        ActionStatusBar.Message = AppStrings.Get("Translation language preference saved.");
    }

    private async void PreviousMonthButton_Click(object sender, RoutedEventArgs e)
    {
        _visibleMonth = _visibleMonth.AddMonths(-1);
        await LoadVisibleMonthAsync();
    }

    private async void NextMonthButton_Click(object sender, RoutedEventArgs e)
    {
        _visibleMonth = _visibleMonth.AddMonths(1);
        await LoadVisibleMonthAsync();
    }

    private async Task RefreshStateAsync()
    {
        if (_backendHost == null)
        {
            SetErrorState(AppStrings.Get("Backend host is not available."));
            return;
        }

        StatusBar.Severity = InfoBarSeverity.Informational;
        StatusBar.Title = AppStrings.Get("Loading backend state");
        StatusBar.Message = AppStrings.Get("Requesting the initial snapshot from apod_wallpaper.Core.");

        var snapshotResult = await _backendHost.GetInitialStateAsync();
        if (!snapshotResult.Succeeded || snapshotResult.Value == null)
        {
            SetErrorState(AppStrings.GetBackendMessageOrDefault(snapshotResult.Error?.Message, "Unable to load the initial snapshot."));
            return;
        }

        _initialStateSnapshot = snapshotResult.Value;
        _trayStatus?.MarkBackendCheck();
        ApplyInitialState(_initialStateSnapshot);

        StatusBar.Severity = InfoBarSeverity.Success;
        StatusBar.Title = AppStrings.Get("Backend initialized");
        StatusBar.Message = AppStrings.Get("The WinUI host created ApplicationController and loaded the initial snapshot in one backend call.");

        await Task.WhenAll(
            LoadVisibleMonthAsync(),
            LoadPreviewForSelectedDateAsync());
        QueueTodayAvailabilityProbe();
    }

    private void ApplyInitialState(apod_wallpaper.ApplicationInitialStateSnapshot snapshot)
    {
        _currentSettingsSnapshot = snapshot.Settings?.Clone() ?? new apod_wallpaper.ApplicationSettingsSnapshot();
        _lastPreviewWorkflow = null;
        var settings = _currentSettingsSnapshot;
        PreferredDateText.Text = snapshot.PreferredDisplayDate.ToString("yyyy-MM-dd");
        WallpaperStyleText.Text = AppStrings.WallpaperStyleName(snapshot.SelectedWallpaperStyle);
        AutoCheckText.Text = settings.AutoRefreshEnabled ? AppStrings.Get("Enabled") : AppStrings.Get("Disabled");
        StartupText.Text = settings.StartWithWindows ? AppStrings.Get("Enabled") : AppStrings.Get("Disabled");
        ApiKeyStateText.Text = FormatApiKeyState(snapshot);
        ImagesDirectoryText.Text = !string.IsNullOrWhiteSpace(snapshot.StoragePaths.ImagesDirectory)
            ? snapshot.StoragePaths.ImagesDirectory
            : AppStrings.Get("Not configured");
        StorageModeText.Text = snapshot.StoragePaths.Mode.ToString();
        LocalImageIndexText.Text = snapshot.LocalImageIndexReady ? AppStrings.Get("Ready") : AppStrings.Get("Not ready yet");
        TrayActionText.Text = settings.TrayDoubleClickAction
            ? AppStrings.Get("Apply latest APOD")
            : AppStrings.Get("Default window action");
        _previewCacheDirectory = Path.Combine(snapshot.StoragePaths.CacheDirectory, "previews");
        Directory.CreateDirectory(_previewCacheDirectory);

        _selectedDate = snapshot.PreferredDisplayDate.Date;
        _visibleMonth = new DateTime(_selectedDate.Year, _selectedDate.Month, 1);
        RefreshSelectedDateText();
        VisibleMonthText.Text = FormatVisibleMonth(_visibleMonth);
        EnsureCalendarMonthBuilt(_visibleMonth);
        UpdateCalendarSelectionOnly();
        SetWallpaperStyleSelection(snapshot.SelectedWallpaperStyle);
        SetAutoRefreshToggleState(settings.AutoRefreshEnabled);
        UpdateActionAvailability();
        ActionStatusBar.Severity = InfoBarSeverity.Informational;
        ActionStatusBar.Title = AppStrings.Get("Actions ready");
        ActionStatusBar.Message = AppStrings.Get("Use Download, Download and apply, or Apply latest. Wallpaper style changes are saved through the backend and can reapply the current image.");

        SnapshotSummaryText.Text = string.Join(Environment.NewLine, new[]
        {
            AppStrings.Get("The calendar is the primary date selector now."),
            AppStrings.Get("Green is always resolved from the local images folder, so deleted files do not lie to the user."),
            AppStrings.Get("Red and blue states come from backend month knowledge persisted in metadata cache."),
            UsesPersonalApiKey(snapshot)
                ? AppStrings.Get("Personal API key detected. Month warmup will refresh missing dates in the background.")
                : AppStrings.Get("DEMO_KEY mode detected. Automatic month warmup is limited to avoid burning the shared rate limit."),
        });
    }

    private async Task LoadVisibleMonthAsync()
    {
        if (_backendHost == null)
            return;

        var month = new DateTime(_visibleMonth.Year, _visibleMonth.Month, 1);
        var requestVersion = Interlocked.Increment(ref _monthRequestVersion);
        VisibleMonthText.Text = FormatVisibleMonth(month);
        EnsureCalendarMonthBuilt(month);
        SetCalendarToLoadingState(month);
        TouchVisibleMonthWindow(month);

        _ = PrewarmWindowAsync(month, requestVersion);

        if (TryGetHotMonthState(month, out var hotState))
        {
            _currentMonthState = hotState;
            await ApplyCalendarMonthStateAsync(_currentMonthState, progressive: false, requestVersion);
            SetMonthReadyState(_currentMonthState, warmed: _warmedMonths.Contains(month));

            if (ShouldWarmMonth(month))
            {
                MonthStatusBar.Severity = InfoBarSeverity.Informational;
                MonthStatusBar.Title = AppStrings.Get("Refreshing month in background");
                MonthStatusBar.Message = BuildMonthWarmupMessage(month);
                _ = WarmMonthAsync(month, requestVersion);
            }

            return;
        }

        MonthStatusBar.Severity = InfoBarSeverity.Informational;
        MonthStatusBar.Title = AppStrings.Get("Loading cached month state");
        MonthStatusBar.Message = AppStrings.Get("Rendering cached knowledge first so the calendar appears immediately.");

        var cachedStopwatch = Stopwatch.StartNew();
        var cachedResult = await GetOrLoadMonthStateAsync(month, refreshMissingDates: false, apod_wallpaper.MonthRefreshMode.Balanced);
        cachedStopwatch.Stop();
        _lastMonthCachedElapsedMs = cachedStopwatch.ElapsedMilliseconds;
        if (requestVersion != _monthRequestVersion)
            return;

        if (!cachedResult.Succeeded || cachedResult.Value == null)
        {
            SetMonthErrorState(month, AppStrings.GetBackendMessageOrDefault(cachedResult.Error?.Message, "Unable to load calendar month state."));
            return;
        }

        _currentMonthState = cachedResult.Value;
        await ApplyCalendarMonthStateAsync(_currentMonthState, progressive: false, requestVersion);
        SetMonthReadyState(_currentMonthState, warmed: false);

        if (!ShouldWarmMonth(month))
            return;

        MonthStatusBar.Severity = InfoBarSeverity.Informational;
        MonthStatusBar.Title = AppStrings.Get("Refreshing month in background");
        MonthStatusBar.Message = BuildMonthWarmupMessage(month);

        _ = WarmMonthAsync(month, requestVersion);
    }

    private async Task WarmMonthAsync(DateTime month, int requestVersion)
    {
        if (_backendHost == null)
            return;

        var refreshStopwatch = Stopwatch.StartNew();
        var refreshedResult = await _backendHost.Backend.GetCalendarMonthStateAsync(month, true, apod_wallpaper.MonthRefreshMode.Aggressive);
        refreshStopwatch.Stop();
        if (requestVersion != _monthRequestVersion)
            return;

        if (!refreshedResult.Succeeded || refreshedResult.Value == null)
        {
            MonthStatusBar.Severity = InfoBarSeverity.Warning;
            MonthStatusBar.Title = AppStrings.Get("Month refresh partially unavailable");
            MonthStatusBar.Message = AppStrings.GetBackendMessageOrDefault(refreshedResult.Error?.Message, "The calendar kept its cached state because background refresh did not finish cleanly.");
            return;
        }

        _currentMonthState = refreshedResult.Value;
        await ApplyCalendarMonthStateAsync(_currentMonthState, progressive: true, requestVersion);
        if (!IsCurrentMonth(month))
            _warmedMonths.Add(month.Date);

        SetMonthReadyState(_currentMonthState, warmed: true, refreshStopwatch.ElapsedMilliseconds);
    }

    private void SetMonthReadyState(apod_wallpaper.ApodCalendarMonthState monthState, bool warmed, long warmElapsedMs = 0)
    {
        var counts = CountMonthStates(monthState);
        MonthStatusBar.Severity = warmed ? InfoBarSeverity.Success : InfoBarSeverity.Informational;
        MonthStatusBar.Title = AppStrings.Get(warmed ? "Month refreshed" : "Month loaded");
        MonthStatusBar.Message = AppStrings.Format(
            warmed
                ? "Local: {0}  Remote image: {1}  Unsupported: {2}  Unknown: {3}  (cache {4} ms, warmup {5} ms)"
                : "Local: {0}  Remote image: {1}  Unsupported: {2}  Unknown: {3}  (cache {4} ms)",
            counts.Local,
            counts.RemoteImage,
            counts.Unsupported,
            counts.Unknown,
            _lastMonthCachedElapsedMs,
            warmElapsedMs);
    }

    private void SetMonthErrorState(DateTime month, string message)
    {
        VisibleMonthText.Text = FormatVisibleMonth(month);
        MonthStatusBar.Severity = InfoBarSeverity.Error;
        MonthStatusBar.Title = AppStrings.Get("Calendar month failed");
        MonthStatusBar.Message = message;
        SelectedDateText.Text = AppStrings.Get("Calendar unavailable");
    }

    private async Task ApplyCalendarMonthStateAsync(apod_wallpaper.ApodCalendarMonthState monthState, bool progressive, int requestVersion)
    {
        EnsureCalendarGridDefinitions();
        EnsureCalendarMonthBuilt(monthState.Month);

        VisibleMonthText.Text = FormatVisibleMonth(monthState.Month);
        RefreshSelectedDateText();

        var monthStart = monthState.Month;
        var effectiveLatestPublishedDate = ResolveEffectiveLatestPublishedDate(monthState.LatestPublishedDate);
        var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
        var changedVisuals = new List<(CalendarDayVisual Visual, apod_wallpaper.ApodCalendarDayState? DayState)>();

        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(monthStart.Year, monthStart.Month, day);
            monthState.TryGetDay(date, out var dayState);

            if (_calendarDayVisuals.TryGetValue(date.Date, out var visual))
            {
                if (NeedsCalendarDayUpdate(visual, dayState, effectiveLatestPublishedDate, isLoading: false))
                    changedVisuals.Add((visual, dayState));
            }
        }

        if (!progressive || changedVisuals.Count <= 1)
        {
            foreach (var changedVisual in changedVisuals)
                UpdateCalendarDayVisual(changedVisual.Visual, changedVisual.Visual.Date, changedVisual.DayState, effectiveLatestPublishedDate, isLoading: false);

            return;
        }

        foreach (var changedVisual in changedVisuals)
        {
            if (requestVersion != _monthRequestVersion)
                return;

            UpdateCalendarDayVisual(changedVisual.Visual, changedVisual.Visual.Date, changedVisual.DayState, effectiveLatestPublishedDate, isLoading: false);
            await Task.Delay(WarmupVisualStepDelayMs);
        }
    }

    private CalendarDayVisual CreateCalendarDayVisual(DateTime date)
    {
        var dayNumberText = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var statusText = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 9,
            Opacity = 0.92,
        };

        var content = new StackPanel
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
        };

        content.Children.Add(dayNumberText);
        content.Children.Add(statusText);

        var button = new Button
        {
            Content = content,
            Padding = new Thickness(4),
            MinHeight = 44,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            CornerRadius = new CornerRadius(10),
            Tag = date,
        };

        button.Click += CalendarDayButton_Click;

        return new CalendarDayVisual
        {
            Date = date.Date,
            Button = button,
            DayNumberText = dayNumberText,
            StatusText = statusText,
            IsLoading = true,
        };
    }

    private void UpdateCalendarDayVisual(
        CalendarDayVisual visual,
        DateTime date,
        apod_wallpaper.ApodCalendarDayState? dayState,
        DateTime latestPublishedDate,
        bool isLoading)
    {
        var isFuture = date.Date > latestPublishedDate.Date;
        var hasLocalImage = dayState?.IsLocalImageAvailable == true;
        var hasRemoteImage = dayState?.IsKnown == true && dayState.HasImage && !hasLocalImage;
        var isUnsupported = dayState?.IsKnown == true && !dayState.HasImage;
        var isUnknown = !hasLocalImage && !hasRemoteImage && !isUnsupported;
        var isSelected = _selectedDate.Date == date.Date;

        var isLightTheme = ActualTheme == ElementTheme.Light;
        var background = ResolveCalendarUnknownBrush(isLightTheme);
        var foreground = ResolveCalendarUnknownForegroundBrush(isLightTheme);
        if (isFuture)
        {
            background = ResolveCalendarFutureBrush(isLightTheme);
            foreground = ResolveCalendarFutureForegroundBrush(isLightTheme);
        }
        else if (hasLocalImage)
        {
            background = CalendarGreenBrush;
            foreground = CalendarDefaultForegroundBrush;
        }
        else if (hasRemoteImage)
        {
            background = CalendarBlueBrush;
            foreground = CalendarDefaultForegroundBrush;
        }
        else if (isUnsupported)
        {
            background = CalendarRedBrush;
            foreground = CalendarDefaultForegroundBrush;
        }

        visual.DayNumberText.Text = date.Day.ToString(CultureInfo.InvariantCulture);
        visual.DayNumberText.FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal;
        visual.StatusText.Text = isLoading
            ? AppStrings.Get(isFuture ? "future" : "loading")
            : BuildMiniStatusLabel(isFuture, hasLocalImage, hasRemoteImage, isUnsupported, isUnknown);

        visual.Button.Background = background;
        visual.Button.Foreground = foreground;
        visual.Button.BorderBrush = isSelected ? ResolveCalendarSelectedBorderBrush(isLightTheme) : background;
        visual.Button.BorderThickness = isSelected ? new Thickness(2) : new Thickness(1);
        visual.Button.IsEnabled = !isFuture;
        visual.Button.Tag = date;
        visual.IsLoading = isLoading;
        visual.CurrentDayState = dayState;
        visual.LatestPublishedDate = latestPublishedDate;
        visual.LastVisualSignature = BuildCalendarDaySignature(date, dayState, latestPublishedDate, isLoading);

        ToolTipService.SetToolTip(
            visual.Button,
            isLoading && !isFuture
                ? date.ToString("dddd, dd MMMM yyyy", AppStrings.DateCulture) + Environment.NewLine + AppStrings.Get("Loading calendar state")
                : BuildDayTooltip(date, isFuture, hasLocalImage, hasRemoteImage, isUnsupported, isUnknown));
    }

    private void QueueTodayAvailabilityProbe()
    {
        if (_backendHost == null)
            return;

        var today = DateTime.Today;
        if (!IsCurrentMonth(_visibleMonth))
            return;

        var existingTask = _todayAvailabilityProbeTask;
        if (existingTask != null && !existingTask.IsCompleted)
            return;

        if (apod_wallpaper.ApodCalendarAvailability.ShouldThrottleProbe(
            today,
            _lastTodayAvailabilityProbeDate,
            _lastTodayAvailabilityProbeUtc,
            DateTime.UtcNow,
            TodayAvailabilityProbeThrottle))
            return;

        _todayAvailabilityProbeTask = MaybeProbeTodayAvailabilityAsync();
    }

    private async Task MaybeProbeTodayAvailabilityAsync()
    {
        try
        {
            await ProbeTodayAvailabilityCoreAsync();
        }
        catch
        {
            // Best-effort UI responsiveness probe only. Scheduler/download/apply paths own real work.
        }
    }

    private async Task ProbeTodayAvailabilityCoreAsync()
    {
        if (_backendHost == null)
            return;

        var today = DateTime.Today;
        _lastTodayAvailabilityProbeDate = today;
        _lastTodayAvailabilityProbeUtc = DateTime.UtcNow;
        var result = await _backendHost.Backend.ProbeApodPageAvailabilityAsync(today);
        if (!result.Succeeded || result.Value == null || !result.Value.IsAvailable)
            return;

        var previousTransientDate = _transientAvailableApodDate;
        _transientAvailableApodDate = today;
        if (previousTransientDate.HasValue && previousTransientDate.Value.Date >= today)
            return;

        await RefreshCalendarAfterAvailabilityProbeAsync(today);
    }

    private async Task RefreshCalendarAfterAvailabilityProbeAsync(DateTime availableDate)
    {
        var availableMonth = new DateTime(availableDate.Year, availableDate.Month, 1);
        if (_currentMonthState != null && _currentMonthState.Month.Date == availableMonth.Date)
            await ApplyCalendarMonthStateAsync(_currentMonthState, progressive: false, _monthRequestVersion);
        else if (_visibleMonth.Date == availableMonth.Date)
            UpdateCalendarSelectionOnly();
    }

    private static SolidColorBrush ResolveCalendarUnknownBrush(bool isLightTheme)
    {
        return isLightTheme ? CalendarLightUnknownBrush : CalendarDarkUnknownBrush;
    }

    private static SolidColorBrush ResolveCalendarUnknownForegroundBrush(bool isLightTheme)
    {
        return isLightTheme ? CalendarLightDefaultForegroundBrush : CalendarDefaultForegroundBrush;
    }

    private static SolidColorBrush ResolveCalendarFutureBrush(bool isLightTheme)
    {
        return isLightTheme ? CalendarLightFutureBrush : CalendarDarkFutureBrush;
    }

    private static SolidColorBrush ResolveCalendarFutureForegroundBrush(bool isLightTheme)
    {
        return isLightTheme ? CalendarLightMutedForegroundBrush : CalendarDarkFutureForegroundBrush;
    }

    private static SolidColorBrush ResolveCalendarSelectedBorderBrush(bool isLightTheme)
    {
        return isLightTheme ? CalendarSelectedLightBorderBrush : CalendarSelectedDarkBorderBrush;
    }

    private async void CalendarDayButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not DateTime date)
            return;

        _selectedDate = date.Date;
        RefreshSelectedDateText();

        if (_currentMonthState != null)
            await ApplyCalendarMonthStateAsync(_currentMonthState, progressive: false, _monthRequestVersion);
        else
            UpdateCalendarSelectionOnly();

        await LoadPreviewForSelectedDateAsync();
    }

    private async Task LoadPreviewForSelectedDateAsync()
    {
        if (_backendHost == null)
            return;

        var selectedDate = _selectedDate.Date;
        var requestVersion = Interlocked.Increment(ref _previewRequestVersion);
        SetPreviewLoadingState(selectedDate);

        var previewStopwatch = Stopwatch.StartNew();
        var operationResult = await _backendHost.Backend.LoadDayAsync(selectedDate);
        previewStopwatch.Stop();
        _lastPreviewBackendElapsedMs = previewStopwatch.ElapsedMilliseconds;
        if (requestVersion != _previewRequestVersion)
            return;

        if (!operationResult.Succeeded || operationResult.Value == null)
        {
            _lastPreviewWorkflow = null;
            UpdateActionAvailability();
            SetPreviewOperationError(selectedDate, operationResult.Error?.Message ?? "Unable to load preview.");
            return;
        }

        var workflow = operationResult.Value;
        _lastPreviewWorkflow = workflow;
        UpdateActionAvailability();
        switch (workflow.Status)
        {
            case apod_wallpaper.ApodWorkflowStatus.Success:
                await SetPreviewSuccessAsync(workflow);
                break;

            case apod_wallpaper.ApodWorkflowStatus.Unavailable:
                SetPreviewUnavailable(workflow);
                break;

            default:
                SetPreviewOperationError(selectedDate, workflow.Message ?? "Unexpected preview workflow state.");
                break;
        }

        if (_selectedDate.Month == _visibleMonth.Month && _selectedDate.Year == _visibleMonth.Year)
            await RefreshVisibleMonthFromCacheAsync();
    }

    private async Task RefreshVisibleMonthFromCacheAsync()
    {
        if (_backendHost == null)
            return;

        var month = new DateTime(_visibleMonth.Year, _visibleMonth.Month, 1);
        var refreshedResult = await GetOrLoadMonthStateAsync(
            month,
            refreshMissingDates: false,
            apod_wallpaper.MonthRefreshMode.Balanced,
            preferHotCache: false);
        if (!refreshedResult.Succeeded || refreshedResult.Value == null)
            return;

        _currentMonthState = refreshedResult.Value;
        RememberMonthState(_currentMonthState);
        await ApplyCalendarMonthStateAsync(_currentMonthState, progressive: false, _monthRequestVersion);
        SetMonthReadyState(_currentMonthState, warmed: false);
    }

    private async Task PrewarmWindowAsync(DateTime visibleMonth, int requestVersion)
    {
        if (_backendHost == null)
            return;

        var monthsToPrewarm = new[]
        {
            visibleMonth.AddMonths(-1),
            visibleMonth,
            visibleMonth.AddMonths(1),
        };

        foreach (var month in monthsToPrewarm)
        {
            var normalizedMonth = new DateTime(month.Year, month.Month, 1);
            if (requestVersion != _monthRequestVersion)
                return;

            if (TryGetHotMonthState(normalizedMonth, out _))
                continue;

            if (!_monthsInFlight.Add(normalizedMonth))
                continue;

            try
            {
                var cachedResult = await GetOrLoadMonthStateAsync(normalizedMonth, refreshMissingDates: false, apod_wallpaper.MonthRefreshMode.Balanced);
                if (!cachedResult.Succeeded || cachedResult.Value == null)
                    continue;

                if (requestVersion != _monthRequestVersion)
                    return;

                RememberMonthState(cachedResult.Value);
            }
            finally
            {
                _monthsInFlight.Remove(normalizedMonth);
            }
        }
    }

    private async Task<apod_wallpaper.OperationResult<apod_wallpaper.ApodCalendarMonthState>> GetOrLoadMonthStateAsync(
        DateTime month,
        bool refreshMissingDates,
        apod_wallpaper.MonthRefreshMode refreshMode,
        bool preferHotCache = true)
    {
        if (_backendHost == null)
            return apod_wallpaper.OperationResult<apod_wallpaper.ApodCalendarMonthState>.Failure(
                new apod_wallpaper.OperationError(
                    apod_wallpaper.OperationErrorCode.InitializationFailed,
                    "Backend host is not available.",
                    false));

        var normalizedMonth = new DateTime(month.Year, month.Month, 1);
        if (preferHotCache && !refreshMissingDates && TryGetHotMonthState(normalizedMonth, out var hotState))
        {
            return apod_wallpaper.OperationResult<apod_wallpaper.ApodCalendarMonthState>.Success(hotState);
        }

        var result = await _backendHost.Backend.GetCalendarMonthStateAsync(normalizedMonth, refreshMissingDates, refreshMode);
        if (result.Succeeded && result.Value != null)
            RememberMonthState(result.Value);

        return result;
    }

    private bool TryGetHotMonthState(DateTime month, out apod_wallpaper.ApodCalendarMonthState state)
    {
        var normalizedMonth = new DateTime(month.Year, month.Month, 1);
        if (_hotMonthCache.TryGetValue(normalizedMonth, out var entry))
        {
            entry.LastAccessStamp = NextMonthCacheAccessStamp();
            state = entry.State;
            return true;
        }

        state = null!;
        return false;
    }

    private void RememberMonthState(apod_wallpaper.ApodCalendarMonthState monthState)
    {
        var normalizedMonth = new DateTime(monthState.Month.Year, monthState.Month.Month, 1);
        _hotMonthCache[normalizedMonth] = new MonthCacheEntry
        {
            State = monthState,
            LastAccessStamp = NextMonthCacheAccessStamp(),
        };

        TrimHotMonthCache();
    }

    private void TrimHotMonthCache()
    {
        if (_hotMonthCache.Count <= HotMonthCacheLimit)
            return;

        var pinnedWindow = BuildPinnedWindow(_visibleMonth);

        while (_hotMonthCache.Count > HotMonthCacheLimit)
        {
            DateTime? oldestKey = null;
            long oldestStamp = long.MaxValue;

            foreach (var pair in _hotMonthCache)
            {
                if (pinnedWindow.Contains(pair.Key))
                    continue;

                if (pair.Value.LastAccessStamp < oldestStamp)
                {
                    oldestStamp = pair.Value.LastAccessStamp;
                    oldestKey = pair.Key;
                }
            }

            if (!oldestKey.HasValue)
                break;

            _hotMonthCache.Remove(oldestKey.Value);
            _warmedMonths.Remove(oldestKey.Value);
        }
    }

    private long NextMonthCacheAccessStamp()
    {
        return Interlocked.Increment(ref _monthCacheAccessStamp);
    }

    private void TouchVisibleMonthWindow(DateTime visibleMonth)
    {
        foreach (var month in BuildPinnedWindow(visibleMonth))
        {
            if (_hotMonthCache.TryGetValue(month, out var entry))
                entry.LastAccessStamp = NextMonthCacheAccessStamp();
        }
    }

    private static HashSet<DateTime> BuildPinnedWindow(DateTime centerMonth)
    {
        var normalizedCenter = new DateTime(centerMonth.Year, centerMonth.Month, 1);
        var pinnedWindow = new HashSet<DateTime>();
        for (var offset = -PinnedMonthWindowRadius; offset <= PinnedMonthWindowRadius; offset++)
        {
            var month = normalizedCenter.AddMonths(offset);
            pinnedWindow.Add(new DateTime(month.Year, month.Month, 1));
        }

        return pinnedWindow;
    }

    private void SetPreviewLoadingState(DateTime selectedDate)
    {
        PreviewStatusBar.Severity = InfoBarSeverity.Informational;
        PreviewStatusBar.Title = AppStrings.Get("Loading preview");
        PreviewStatusBar.Message = AppStrings.Format("Loading APOD preview for {0}.", selectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        ResolvedDateText.Text = AppStrings.Get("Loading...");
        PreviewWorkflowStatusText.Text = AppStrings.Get("Loading");
        PreviewSourceText.Text = AppStrings.Get("Resolving...");
        PreviewLocationText.Text = AppStrings.Get("Resolving preview location...");
        PreviewMessageText.Text = AppStrings.Get("The UI will ignore stale results if another date is requested before this one finishes.");

        PreviewImageBrush.ImageSource = null;
        PreviewImageFrame.Visibility = Visibility.Collapsed;
        PreviewPlaceholderPanel.Visibility = Visibility.Collapsed;
        PreviewProgressRing.IsActive = true;
        PreviewProgressRing.Visibility = Visibility.Visible;
        PreviewTitleText.Text = AppStrings.Get("Explanation");
        SetPreviewFallbackBody(AppStrings.Get("Loading APOD preview..."));
        ResetPreviewFrame();
    }

    private async Task SetPreviewSuccessAsync(apod_wallpaper.ApodWorkflowResult workflow)
    {
        await SetPreviewSuccessAsync(workflow, preserveRenderedPreview: false);
    }

    private async Task SetPreviewSuccessAsync(apod_wallpaper.ApodWorkflowResult workflow, bool preserveRenderedPreview)
    {
        PreviewStatusBar.Severity = InfoBarSeverity.Success;
        PreviewStatusBar.Title = AppStrings.Get("Preview loaded");
        PreviewStatusBar.Message = AppStrings.Format(
            "{0} Backend: {1} ms. Rendering preview image...",
            AppStrings.GetBackendMessageOrDefault(workflow.Message, "Preview metadata loaded successfully."),
            _lastPreviewBackendElapsedMs);

        ResolvedDateText.Text = workflow.ResolvedDate.HasValue
            ? workflow.ResolvedDate.Value.ToString("yyyy-MM-dd")
            : workflow.RequestedDate.ToString("yyyy-MM-dd");
        PreviewWorkflowStatusText.Text = AppStrings.Get(workflow.Status.ToString());
        PreviewSourceText.Text = AppStrings.Get(workflow.Source.ToString());
        PreviewLocationText.Text = workflow.PreviewLocation ?? AppStrings.Get("No preview location");
        PreviewMessageText.Text = string.IsNullOrWhiteSpace(workflow.Message)
            ? AppStrings.Get("Success")
            : AppStrings.Get(workflow.Message);
        PreviewTitleText.Text = AppStrings.Get("Explanation");
        var originalExplanation = ResolveOriginalExplanationText(workflow);
        if (string.IsNullOrWhiteSpace(originalExplanation))
            SetPreviewFallbackBody(ResolvePreviewBody(workflow));
        else
            SetExplanationBody(originalExplanation, originalExplanation);
        UpdateActionAvailability();

        if (preserveRenderedPreview)
        {
            PreviewProgressRing.IsActive = false;
            PreviewProgressRing.Visibility = Visibility.Collapsed;
            PreviewStatusBar.Title = AppStrings.Get("Preview preserved");
            PreviewStatusBar.Message = AppStrings.Format(
                "{0} Backend: {1} ms. Existing preview reused without rerender.",
                AppStrings.GetBackendMessageOrDefault(workflow.Message, "Preview metadata loaded successfully."),
                _lastPreviewBackendElapsedMs);
            return;
        }

        var imageLoaded = await TryShowPreviewImageAsync(workflow.PreviewLocation);
        PreviewProgressRing.IsActive = false;
        PreviewProgressRing.Visibility = Visibility.Collapsed;

        if (!imageLoaded)
        {
            PreviewPlaceholderTitleText.Text = AppStrings.Get("Preview image unavailable");
            PreviewPlaceholderText.Text = AppStrings.Get("The backend returned a successful workflow, but the preview image could not be opened by the host.");
            PreviewPlaceholderIcon.Glyph = "\uE171";
            PreviewPlaceholderPanel.Visibility = Visibility.Visible;
            PreviewStatusBar.Severity = InfoBarSeverity.Warning;
            PreviewStatusBar.Title = AppStrings.Get("Preview metadata loaded");
            PreviewStatusBar.Message = AppStrings.Get("Workflow succeeded, but the image did not render in the host.");
        }
    }

    private void SetPreviewUnavailable(apod_wallpaper.ApodWorkflowResult workflow)
    {
        PreviewProgressRing.IsActive = false;
        PreviewProgressRing.Visibility = Visibility.Collapsed;
        PreviewImageBrush.ImageSource = null;
        PreviewImageFrame.Visibility = Visibility.Collapsed;
        PreviewPlaceholderPanel.Visibility = Visibility.Visible;
        ResetPreviewFrame();

        PreviewStatusBar.Severity = InfoBarSeverity.Warning;
        PreviewStatusBar.Title = AppStrings.Get("Preview unavailable");
        PreviewStatusBar.Message = AppStrings.GetBackendMessageOrDefault(workflow.Message, "The selected APOD date is currently unavailable.");

        ResolvedDateText.Text = workflow.ResolvedDate.HasValue
            ? workflow.ResolvedDate.Value.ToString("yyyy-MM-dd")
            : AppStrings.Get("Unavailable");
        PreviewWorkflowStatusText.Text = AppStrings.Get(workflow.Status.ToString());
        PreviewSourceText.Text = AppStrings.Get(workflow.Source.ToString());
        PreviewLocationText.Text = AppStrings.Get("No preview image");
        PreviewMessageText.Text = AppStrings.GetBackendMessageOrDefault(workflow.Message, "Unavailable");
        PreviewTitleText.Text = AppStrings.Get("Explanation");
        SetPreviewFallbackBody(ResolveUnavailablePreviewBody(workflow));
        UpdateActionAvailability();

        PreviewPlaceholderTitleText.Text = AppStrings.Get("No image preview");
        PreviewPlaceholderText.Text = AppStrings.GetBackendMessageOrDefault(workflow.Message, "The selected date does not currently resolve to previewable image content.");
        PreviewPlaceholderIcon.Glyph = ResolvePlaceholderGlyph(workflow.Message);
    }

    private void SetPreviewOperationError(DateTime selectedDate, string message)
    {
        PreviewProgressRing.IsActive = false;
        PreviewProgressRing.Visibility = Visibility.Collapsed;
        PreviewImageBrush.ImageSource = null;
        PreviewImageFrame.Visibility = Visibility.Collapsed;
        PreviewPlaceholderPanel.Visibility = Visibility.Visible;
        ResetPreviewFrame();

        PreviewStatusBar.Severity = InfoBarSeverity.Error;
        PreviewStatusBar.Title = AppStrings.Get("Preview failed");
        PreviewStatusBar.Message = message;

        ResolvedDateText.Text = AppStrings.Get("Error");
        PreviewWorkflowStatusText.Text = AppStrings.Get("Error");
        PreviewSourceText.Text = AppStrings.Get("Unknown");
        PreviewLocationText.Text = AppStrings.Get("No preview image");
        PreviewMessageText.Text = message;
        PreviewTitleText.Text = AppStrings.Get("Explanation");
        SetPreviewFallbackBody(AppStrings.Get("We couldn't load this preview right now."));
        UpdateActionAvailability();

        PreviewPlaceholderTitleText.Text = AppStrings.Get("Preview failed");
        PreviewPlaceholderText.Text = message;
        PreviewPlaceholderIcon.Glyph = "\uE783";
    }

    private async void WallpaperStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressWallpaperStyleSelectionChanged || _backendHost == null || _currentSettingsSnapshot == null)
            return;

        var selectedStyle = GetSelectedWallpaperStyle();
        if (_currentSettingsSnapshot.WallpaperStyleIndex == (int)selectedStyle)
            return;

        var updatedSettings = await GetFreshSettingsSnapshotAsync();
        updatedSettings.WallpaperStyleIndex = (int)selectedStyle;

        ActionStatusBar.Severity = InfoBarSeverity.Informational;
        ActionStatusBar.Title = AppStrings.Get("Saving wallpaper style");
        ActionStatusBar.Message = AppStrings.Get("Persisting wallpaper style through the backend facade.");

        var saveResult = await _backendHost.Backend.SaveSettingsAsync(updatedSettings);
        if (!saveResult.Succeeded || saveResult.Value == null)
        {
            ActionStatusBar.Severity = InfoBarSeverity.Error;
            ActionStatusBar.Title = AppStrings.Get("Wallpaper style was not saved");
            ActionStatusBar.Message = AppStrings.GetBackendMessageOrDefault(saveResult.Error?.Message, "Unable to persist wallpaper style.");
            SetWallpaperStyleSelection(ResolveWallpaperStyleFromSettings(_currentSettingsSnapshot));
            return;
        }

        _currentSettingsSnapshot = saveResult.Value;
        WallpaperStyleText.Text = AppStrings.WallpaperStyleName(selectedStyle);
        if (_initialStateSnapshot != null)
        {
            _initialStateSnapshot.Settings = saveResult.Value.Clone();
            _initialStateSnapshot.SelectedWallpaperStyle = selectedStyle;
        }

        ActionStatusBar.Severity = InfoBarSeverity.Success;
        ActionStatusBar.Title = AppStrings.Get("Wallpaper style saved");
        ActionStatusBar.Message = AppStrings.Format("Future apply actions will use {0}.", AppStrings.WallpaperStyleName(selectedStyle));

        if (_lastPreviewWorkflow?.Status == apod_wallpaper.ApodWorkflowStatus.Success && _lastPreviewWorkflow.Entry?.HasImage == true)
        {
            await ExecuteWallpaperActionAsync(
                "Reapplying current image",
                AppStrings.Format("Wallpaper style changed to {0}. Reapplying the selected image.", AppStrings.WallpaperStyleName(selectedStyle)),
                () => _backendHost.Backend.ApplyDayAsync(_selectedDate, selectedStyle),
                updateSelectionFromWorkflow: false);
        }
    }

    private async Task<bool> TryShowPreviewImageAsync(string? previewLocation)
    {
        PreviewImageBrush.ImageSource = null;
        PreviewImageFrame.Visibility = Visibility.Collapsed;
        PreviewPlaceholderPanel.Visibility = Visibility.Collapsed;
        _pendingPreviewLocation = null;
        _previewImageStopwatch.Reset();
        ResetPreviewFrame();

        if (string.IsNullOrWhiteSpace(previewLocation))
            return false;

        try
        {
            var resolvedPreviewLocation = await ResolvePreviewAssetLocationAsync(previewLocation);
            var bitmap = new BitmapImage();
            bitmap.DecodePixelWidth = ResolvePreviewDecodeWidth();
            bitmap.ImageOpened += PreviewBitmap_ImageOpened;
            bitmap.ImageFailed += PreviewBitmap_ImageFailed;
            _pendingPreviewLocation = resolvedPreviewLocation;
            _previewImageStopwatch.Restart();
            var previewUri = BuildPreviewUri(resolvedPreviewLocation);

            if (previewUri.IsFile)
            {
                var previewFile = await StorageFile.GetFileFromPathAsync(previewUri.LocalPath);
                using IRandomAccessStream stream = await previewFile.OpenReadAsync();
                await bitmap.SetSourceAsync(stream);
                UpdatePreviewFrameMetrics(bitmap);
                PreviewImageBrush.ImageSource = bitmap;
                CompletePreviewImageRender();
            }
            else
            {
                bitmap.UriSource = previewUri;
                PreviewImageBrush.ImageSource = bitmap;
            }

            PreviewImageFrame.Visibility = Visibility.Visible;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void RefreshSelectedDateText()
    {
        SelectedDateText.Text = AppStrings.Format("Selected date: {0}", _selectedDate.ToString("dddd, dd MMMM yyyy", AppStrings.DateCulture));
    }

    private static string FormatVisibleMonth(DateTime month)
    {
        return month.ToString("MMMM yyyy", AppStrings.DateCulture);
    }

    private apod_wallpaper.WallpaperStyle GetSelectedWallpaperStyle()
    {
        return WallpaperStyleComboBox.SelectedItem is apod_wallpaper.WallpaperStyle selectedStyle
            ? selectedStyle
            : ResolveWallpaperStyleFromSettings(_currentSettingsSnapshot);
    }

    private static apod_wallpaper.WallpaperStyle ResolveWallpaperStyleFromSettings(apod_wallpaper.ApplicationSettingsSnapshot? settings)
    {
        if (settings == null)
            return apod_wallpaper.WallpaperStyle.Smart;

        if (Enum.IsDefined(typeof(apod_wallpaper.WallpaperStyle), settings.WallpaperStyleIndex))
            return (apod_wallpaper.WallpaperStyle)settings.WallpaperStyleIndex;

        return apod_wallpaper.WallpaperStyle.Smart;
    }

    private void SetWallpaperStyleSelection(apod_wallpaper.WallpaperStyle style)
    {
        _suppressWallpaperStyleSelectionChanged = true;
        try
        {
            WallpaperStyleComboBox.SelectedItem = style;
        }
        finally
        {
            _suppressWallpaperStyleSelectionChanged = false;
        }
    }

    private void SetAutoRefreshToggleState(bool isEnabled)
    {
        _suppressAutoRefreshToggleChanged = true;
        try
        {
            AutoRefreshToggleButton.IsChecked = isEnabled;
        }
        finally
        {
            _suppressAutoRefreshToggleChanged = false;
        }

        UpdateAutoRefreshToggleVisual();
    }

    private void UpdateAutoRefreshToggleVisual()
    {
        var isEnabled = AutoRefreshToggleButton.IsChecked == true;
        AutoRefreshToggleButton.Background = isEnabled ? AutoRefreshEnabledBrush : AutoRefreshDisabledBrush;
        AutoRefreshToggleButton.BorderBrush = isEnabled ? AutoRefreshEnabledBrush : AutoRefreshDisabledBrush;
        AutoRefreshToggleButton.Foreground = AutoRefreshForegroundBrush;
        AutoRefreshToggleButton.Content = isEnabled ? AppStrings.Get("Auto On") : AppStrings.Get("Auto Off");
    }

    private void UpdateActionAvailability()
    {
        var hasBackend = _backendHost != null;
        var hasSuccessfulPreview = _lastPreviewWorkflow?.Status == apod_wallpaper.ApodWorkflowStatus.Success;

        ReloadPreviewButton.IsEnabled = hasBackend && !_isApplyingWallpaperAction;
        OpenNasaPageButton.IsEnabled = hasBackend && !_isApplyingWallpaperAction;
        NasaPageButton.IsEnabled = hasBackend && !_isApplyingWallpaperAction;
        WallpaperStyleComboBox.IsEnabled = hasBackend && !_isApplyingWallpaperAction;
        DownloadOnlyButton.IsEnabled = hasBackend && !_isApplyingWallpaperAction;
        DownloadAndApplyButton.IsEnabled = hasBackend && !_isApplyingWallpaperAction;
        AutoRefreshToggleButton.IsEnabled = hasBackend && _currentSettingsSnapshot != null && !_isApplyingWallpaperAction;
        ApplyLatestButton.IsEnabled = hasBackend && !_isApplyingWallpaperAction;

        if (!hasSuccessfulPreview && !_isApplyingWallpaperAction)
            WallpaperStyleComboBox.IsEnabled = hasBackend && _currentSettingsSnapshot != null;

        UpdateExplanationActionState();
    }

    private void SetExplanationBody(string displayedText, string originalText, bool isTranslated = false)
    {
        _originalExplanationText = (originalText ?? string.Empty).Trim();
        _displayedExplanationText = (displayedText ?? string.Empty).Trim();
        _isExplanationTranslated = isTranslated && !string.IsNullOrWhiteSpace(_displayedExplanationText);
        PreviewBodyText.Text = _displayedExplanationText;
        UpdateExplanationActionState();
    }

    private void SetPreviewFallbackBody(string text)
    {
        _originalExplanationText = string.Empty;
        _displayedExplanationText = string.Empty;
        _isExplanationTranslated = false;
        PreviewBodyText.Text = text;
        UpdateExplanationActionState();
    }

    private void UpdateExplanationActionState()
    {
        var hasDisplayedExplanationText = !string.IsNullOrWhiteSpace(_displayedExplanationText);
        var hasOriginalExplanationText = !string.IsNullOrWhiteSpace(_originalExplanationText);
        var selectedTargetLanguage = GetSelectedTranslationTargetLanguage();
        var hasSelectedTargetLanguage = !string.IsNullOrEmpty(selectedTargetLanguage);

        CopyExplanationButton.IsEnabled = hasDisplayedExplanationText;
        TranslateExplanationButton.IsEnabled = hasOriginalExplanationText && hasSelectedTargetLanguage;

        ToolTipService.SetToolTip(
            CopyExplanationButton,
            hasDisplayedExplanationText ? AppStrings.Get("Copy") : AppStrings.Get("No text to copy"));

        var translateTooltip = !hasSelectedTargetLanguage
            ? AppStrings.Get("Select a translation language first")
            : hasOriginalExplanationText
                ? AppStrings.Get("Open in Google Translate")
                : AppStrings.Get("No text to translate");
        ToolTipService.SetToolTip(TranslateExplanationButton, translateTooltip);

        ToolTipService.SetToolTip(TranslationTargetLanguageButton, BuildTranslationTargetLanguageTooltip(selectedTargetLanguage));
        AutomationProperties.SetName(CopyExplanationButton, AppStrings.Get("Copy"));
        AutomationProperties.SetHelpText(CopyExplanationButton, hasDisplayedExplanationText ? AppStrings.Get("Copy") : AppStrings.Get("No text to copy"));
        AutomationProperties.SetName(TranslateExplanationButton, AppStrings.Get("Translate"));
        AutomationProperties.SetHelpText(TranslateExplanationButton, translateTooltip);
        AutomationProperties.SetName(TranslationTargetLanguageButton, AppStrings.Get("Translation language"));
        AutomationProperties.SetHelpText(TranslationTargetLanguageButton, BuildTranslationTargetLanguageTooltip(selectedTargetLanguage));
    }

    private void RebuildTranslationTargetLanguageFlyout()
    {
        TranslationTargetLanguageFlyout.Items.Clear();
        foreach (var option in TranslationTargetLanguages)
        {
            var item = new MenuFlyoutItem
            {
                Text = option.Code,
                Tag = option.Code,
            };
            ToolTipService.SetToolTip(item, AppStrings.Get(option.NameKey));
            AutomationProperties.SetName(item, option.Code);
            AutomationProperties.SetHelpText(item, AppStrings.Get(option.NameKey));
            item.Click += TranslationTargetLanguageMenuItem_Click;
            TranslationTargetLanguageFlyout.Items.Add(item);
        }
    }

    private void UpdateTranslationTargetLanguageSelector()
    {
        var selectedLanguage = GetSelectedTranslationTargetLanguage();
        var selectedOption = ResolveTranslationTargetLanguageOption(selectedLanguage);
        TranslationTargetLanguageText.Text = selectedOption?.Code ?? apod_wallpaper.TranslationTargetLanguage.Russian;
    }

    private string GetSelectedTranslationTargetLanguage()
    {
        return apod_wallpaper.ApplicationSettingsSnapshot.NormalizeTranslationTargetLanguage(_currentSettingsSnapshot?.TranslationTargetLanguage);
    }

    private static TranslationTargetLanguageOption? ResolveTranslationTargetLanguageOption(string language)
    {
        foreach (var option in TranslationTargetLanguages)
        {
            if (string.Equals(option.Code, language, StringComparison.Ordinal))
                return option;
        }

        return null;
    }

    private static string BuildTranslationTargetLanguageTooltip(string language)
    {
        var selectedOption = ResolveTranslationTargetLanguageOption(language);
        var languageName = selectedOption != null
            ? AppStrings.Get(selectedOption.NameKey)
            : AppStrings.Get("Russian");
        return AppStrings.Format("{0}: {1}", AppStrings.Get("Translation language"), languageName);
    }

    private bool TryCopyTextToClipboard(string text)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
            Clipboard.Flush();
            return true;
        }
        catch
        {
            ActionStatusBar.Severity = InfoBarSeverity.Error;
            ActionStatusBar.Title = AppStrings.Get("CopyFailed");
            ActionStatusBar.Message = string.Empty;
            return false;
        }
    }

    private void ShowTranslateOpenFailed()
    {
        ActionStatusBar.Severity = InfoBarSeverity.Error;
        ActionStatusBar.Title = AppStrings.Get("Could not open Google Translate");
        ActionStatusBar.Message = string.Empty;
    }

    private async Task ExecuteWallpaperActionAsync(
        string title,
        string message,
        Func<Task<apod_wallpaper.OperationResult<apod_wallpaper.ApodWorkflowResult>>> action,
        bool updateSelectionFromWorkflow = true,
        bool disableAutoRefreshOnSuccess = false)
    {
        if (_backendHost == null || _isApplyingWallpaperAction)
            return;

        _isApplyingWallpaperAction = true;
        UpdateActionAvailability();
        ActionProgressRing.IsActive = true;
        ActionProgressRing.Opacity = 1;
        ActionStatusBar.Severity = InfoBarSeverity.Informational;
        ActionStatusBar.Title = title;
        ActionStatusBar.Message = message;

        try
        {
            var previousWorkflow = _lastPreviewWorkflow;
            var operationResult = await action();
            if (!operationResult.Succeeded || operationResult.Value == null)
            {
                ActionStatusBar.Severity = InfoBarSeverity.Error;
                ActionStatusBar.Title = AppStrings.Format("{0} failed", title);
                ActionStatusBar.Message = AppStrings.GetBackendMessageOrDefault(operationResult.Error?.Message, "The backend did not complete the requested action.");
                return;
            }

            var workflow = operationResult.Value;
            _lastPreviewWorkflow = workflow;

            if (workflow.Status == apod_wallpaper.ApodWorkflowStatus.Unavailable)
            {
                ActionStatusBar.Severity = InfoBarSeverity.Warning;
                ActionStatusBar.Title = AppStrings.Format("{0} unavailable", title);
                ActionStatusBar.Message = AppStrings.GetBackendMessageOrDefault(workflow.Message, "The selected APOD entry does not contain downloadable image content.");
                SetPreviewUnavailable(workflow);
                return;
            }

            if (updateSelectionFromWorkflow && workflow.ResolvedDate.HasValue)
            {
                _selectedDate = workflow.ResolvedDate.Value.Date;
                RefreshSelectedDateText();
                UpdateCalendarSelectionOnly();
            }

            var preserveRenderedPreview = CanReuseRenderedPreview(previousWorkflow, workflow);
            await SetPreviewSuccessAsync(workflow, preserveRenderedPreview);
            if (disableAutoRefreshOnSuccess)
                await DisableAutoRefreshAfterManualApplyAsync(workflow);
            await RefreshMonthStateAfterWorkflowAsync(workflow);

            ActionStatusBar.Severity = InfoBarSeverity.Success;
            ActionStatusBar.Title = AppStrings.Format("{0} complete", title);
            ActionStatusBar.Message = BuildActionSuccessMessage(workflow);
        }
        finally
        {
            ActionProgressRing.IsActive = false;
            ActionProgressRing.Opacity = 0;
            _isApplyingWallpaperAction = false;
            UpdateActionAvailability();
        }
    }

    private async Task SaveAutoRefreshFromMainAsync(bool isEnabled)
    {
        if (_suppressAutoRefreshToggleChanged || _backendHost == null || _currentSettingsSnapshot == null)
            return;

        if (_currentSettingsSnapshot.AutoRefreshEnabled == isEnabled)
        {
            UpdateAutoRefreshToggleVisual();
            return;
        }

        var updatedSnapshot = await GetFreshSettingsSnapshotAsync();
        updatedSnapshot.AutoRefreshEnabled = isEnabled;

        ActionStatusBar.Severity = InfoBarSeverity.Informational;
        ActionStatusBar.Title = AppStrings.Get(isEnabled ? "Enabling auto-check" : "Disabling auto-check");
        ActionStatusBar.Message = AppStrings.Get("Persisting automatic daily check through the backend facade.");

        var saveResult = await _backendHost.Backend.SaveSettingsAsync(updatedSnapshot);
        if (!saveResult.Succeeded || saveResult.Value == null)
        {
            ActionStatusBar.Severity = InfoBarSeverity.Error;
            ActionStatusBar.Title = AppStrings.Get("Auto-check was not updated");
            ActionStatusBar.Message = AppStrings.GetBackendMessageOrDefault(saveResult.Error?.Message, "Unable to save the auto-check preference.");
            SetAutoRefreshToggleState(_currentSettingsSnapshot.AutoRefreshEnabled);
            return;
        }

        ApplySavedSettingsSnapshot(saveResult.Value);
        ActionStatusBar.Severity = InfoBarSeverity.Success;
        ActionStatusBar.Title = AppStrings.Get("Auto-check updated");
        ActionStatusBar.Message = isEnabled
            ? AppStrings.Get("Automatic daily check and apply is now enabled.")
            : AppStrings.Get("Automatic daily check and apply is now disabled.");
    }

    private async Task DisableAutoRefreshAfterManualApplyAsync(apod_wallpaper.ApodWorkflowResult workflow)
    {
        if (_backendHost == null || _currentSettingsSnapshot == null || !_currentSettingsSnapshot.AutoRefreshEnabled)
            return;

        var updatedSnapshot = await GetFreshSettingsSnapshotAsync();
        updatedSnapshot.AutoRefreshEnabled = false;

        var saveResult = await _backendHost.Backend.SaveSettingsAsync(updatedSnapshot);
        if (!saveResult.Succeeded || saveResult.Value == null)
            return;

        ApplySavedSettingsSnapshot(saveResult.Value);
        ActionStatusBar.Severity = InfoBarSeverity.Success;
        ActionStatusBar.Title = AppStrings.Get("Applying wallpaper complete");
        ActionStatusBar.Message = BuildActionSuccessMessage(workflow) + " " + AppStrings.Get("Auto-check was turned off because you manually applied a specific date.");
    }

    private void ApplySavedSettingsSnapshot(apod_wallpaper.ApplicationSettingsSnapshot savedSettings)
    {
        _currentSettingsSnapshot = savedSettings;
        AutoCheckText.Text = AppStrings.Get(savedSettings.AutoRefreshEnabled ? "Enabled" : "Disabled");
        StartupText.Text = AppStrings.Get(savedSettings.StartWithWindows ? "Enabled" : "Disabled");
        TrayActionText.Text = savedSettings.TrayDoubleClickAction
            ? AppStrings.Get("Apply latest APOD")
            : AppStrings.Get("Default window action");

        if (_initialStateSnapshot != null)
            _initialStateSnapshot.Settings = savedSettings.Clone();

        SetAutoRefreshToggleState(savedSettings.AutoRefreshEnabled);
        UpdateTranslationTargetLanguageSelector();
        UpdateActionAvailability();
    }

    private bool CanReuseRenderedPreview(
        apod_wallpaper.ApodWorkflowResult? previousWorkflow,
        apod_wallpaper.ApodWorkflowResult currentWorkflow)
    {
        if (previousWorkflow == null)
            return false;

        if (previousWorkflow.Status != apod_wallpaper.ApodWorkflowStatus.Success ||
            currentWorkflow.Status != apod_wallpaper.ApodWorkflowStatus.Success)
            return false;

        if (PreviewImageBrush.ImageSource == null || PreviewImageFrame.Visibility != Visibility.Visible)
            return false;

        var previousResolvedDate = previousWorkflow.ResolvedDate ?? previousWorkflow.RequestedDate;
        var currentResolvedDate = currentWorkflow.ResolvedDate ?? currentWorkflow.RequestedDate;
        if (previousResolvedDate.Date != currentResolvedDate.Date)
            return false;

        return string.Equals(previousWorkflow.PreviewLocation, currentWorkflow.PreviewLocation, StringComparison.OrdinalIgnoreCase);
    }

    private async Task RefreshMonthStateAfterWorkflowAsync(apod_wallpaper.ApodWorkflowResult workflow)
    {
        InvalidateCalendarMonthsForWorkflow(workflow);

        if (workflow.ResolvedDate.HasValue)
        {
            var resolvedMonth = new DateTime(workflow.ResolvedDate.Value.Year, workflow.ResolvedDate.Value.Month, 1);
            if (resolvedMonth == _visibleMonth)
            {
                await RefreshVisibleMonthFromCacheAsync();
            }
            else
            {
                _visibleMonth = resolvedMonth;
                await LoadVisibleMonthAsync();
            }
        }
        else if (_selectedDate.Month == _visibleMonth.Month && _selectedDate.Year == _visibleMonth.Year)
        {
            await RefreshVisibleMonthFromCacheAsync();
        }
    }

    private static string BuildActionSuccessMessage(apod_wallpaper.ApodWorkflowResult workflow)
    {
        var resolvedDate = workflow.ResolvedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            ?? workflow.RequestedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (workflow.DownloadedNow)
            return "Resolved " + resolvedDate + " and downloaded a fresh local image.";

        if (workflow.IsLocalFile)
            return "Resolved " + resolvedDate + " using a local image file.";

        return "Resolved " + resolvedDate + " successfully.";
    }

    private void PreviewBitmap_ImageOpened(object sender, RoutedEventArgs e)
    {
        if (sender is BitmapImage bitmap)
            UpdatePreviewFrameMetrics(bitmap);

        CompletePreviewImageRender();
    }

    private async Task EnsureWallpaperAppliedSubscriptionAsync()
    {
        if (_backendHost == null || _wallpaperAppliedSubscription != null || _wallpaperAppliedSubscriptionRequested)
            return;

        _wallpaperAppliedSubscriptionRequested = true;
        var subscriptionResult = await _backendHost.Backend.SubscribeWallpaperAppliedAsync(OnWallpaperApplied);
        if (subscriptionResult.Succeeded && subscriptionResult.Value != null)
            _wallpaperAppliedSubscription = subscriptionResult.Value;

        _wallpaperAppliedSubscriptionRequested = false;
    }

    private void OnWallpaperApplied(object? sender, apod_wallpaper.WallpaperAppliedEventArgs e)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(() => _ = HandleWallpaperAppliedAsync(e));
            return;
        }

        _ = HandleWallpaperAppliedAsync(e);
    }

    private async Task HandleWallpaperAppliedAsync(apod_wallpaper.WallpaperAppliedEventArgs e)
    {
        if (e == null || e.Result == null || !e.Automatic)
            return;

        var affectedMonths = InvalidateCalendarMonthsForWorkflow(e.Result);
        await RefreshSettingsSnapshotPreservingCalendarAsync();

        var resolvedDate = e.Result.ResolvedDate ?? e.Result.RequestedDate;
        var resolvedMonth = new DateTime(resolvedDate.Year, resolvedDate.Month, 1);
        var selectionChanged = _selectedDate.Date != resolvedDate.Date;

        _selectedDate = resolvedDate.Date;
        _visibleMonth = resolvedMonth;
        RefreshSelectedDateText();
        VisibleMonthText.Text = FormatVisibleMonth(_visibleMonth);

        if (affectedMonths.Contains(_visibleMonth))
            await RefreshVisibleMonthFromCacheAsync();
        else
            await LoadVisibleMonthAsync();

        if (selectionChanged || _lastPreviewWorkflow?.Status != apod_wallpaper.ApodWorkflowStatus.Success)
        {
            await LoadPreviewForSelectedDateAsync();
        }
    }

    private HashSet<DateTime> InvalidateCalendarMonthsForWorkflow(apod_wallpaper.ApodWorkflowResult workflow)
    {
        var affectedMonths = GetAffectedCalendarMonths(workflow);
        foreach (var month in affectedMonths)
        {
            _hotMonthCache.Remove(month);
            _warmedMonths.Remove(month);
        }

        return affectedMonths;
    }

    private static HashSet<DateTime> GetAffectedCalendarMonths(apod_wallpaper.ApodWorkflowResult workflow)
    {
        var months = new HashSet<DateTime>();
        if (workflow == null)
            return months;

        AddAffectedMonth(months, workflow.RequestedDate);
        if (workflow.ResolvedDate.HasValue)
            AddAffectedMonth(months, workflow.ResolvedDate.Value);
        if (workflow.LatestPublishedDate.HasValue)
            AddAffectedMonth(months, workflow.LatestPublishedDate.Value);

        if (workflow.Entry != null &&
            DateTime.TryParse(workflow.Entry.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var entryDate))
        {
            AddAffectedMonth(months, entryDate);
        }

        return months;
    }

    private static void AddAffectedMonth(HashSet<DateTime> months, DateTime date)
    {
        months.Add(new DateTime(date.Year, date.Month, 1));
    }

    private void CompletePreviewImageRender()
    {
        if (!_previewImageStopwatch.IsRunning)
            return;

        _previewImageStopwatch.Stop();
        PreviewStatusBar.Severity = InfoBarSeverity.Success;
        PreviewStatusBar.Title = AppStrings.Get("Preview rendered");
        PreviewStatusBar.Message = AppStrings.Format(
            "Backend: {0} ms, image render: {1} ms.",
            _lastPreviewBackendElapsedMs,
            _previewImageStopwatch.ElapsedMilliseconds);
    }

    private void PreviewBitmap_ImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        if (_previewImageStopwatch.IsRunning)
            _previewImageStopwatch.Stop();

        PreviewStatusBar.Severity = InfoBarSeverity.Warning;
        PreviewStatusBar.Title = AppStrings.Get("Preview metadata loaded");
        PreviewStatusBar.Message = AppStrings.Format(
            "Backend: {0} ms. The preview image failed to render in the host.",
            _lastPreviewBackendElapsedMs);

        PreviewPlaceholderTitleText.Text = AppStrings.Get("Preview image unavailable");
        PreviewPlaceholderText.Text = AppStrings.Format("The backend resolved preview metadata, but WinUI could not render the image from {0}.", _pendingPreviewLocation ?? AppStrings.Get("the resolved source"));
        PreviewPlaceholderIcon.Glyph = "\uE171";
        PreviewPlaceholderPanel.Visibility = Visibility.Visible;
        PreviewImageFrame.Visibility = Visibility.Collapsed;
        ResetPreviewFrame();
    }

    private static Uri BuildPreviewUri(string previewLocation)
    {
        if (previewLocation.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            previewLocation.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(previewLocation, UriKind.Absolute);
        }

        if (Path.IsPathRooted(previewLocation))
            return new Uri(previewLocation, UriKind.Absolute);

        return new Uri(previewLocation, UriKind.RelativeOrAbsolute);
    }

    private async Task<string> ResolvePreviewAssetLocationAsync(string previewLocation)
    {
        if (string.IsNullOrWhiteSpace(previewLocation))
            return previewLocation;

        if (!previewLocation.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !previewLocation.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return previewLocation;

        var cachedPreviewPath = TryGetPreviewAssetCachePath(previewLocation);
        if (string.IsNullOrWhiteSpace(cachedPreviewPath))
            return previewLocation;

        if (File.Exists(cachedPreviewPath))
            return cachedPreviewPath;

        var downloadedPath = await GetOrDownloadPreviewAssetAsync(previewLocation, cachedPreviewPath);
        return !string.IsNullOrWhiteSpace(downloadedPath) && File.Exists(downloadedPath)
            ? downloadedPath
            : previewLocation;
    }

    private async Task<string?> GetOrDownloadPreviewAssetAsync(string previewUrl, string cachePath)
    {
        Task<string?>? downloadTask = null;
        lock (_previewAssetSyncRoot)
        {
            if (!_previewAssetTasks.TryGetValue(previewUrl, out downloadTask))
            {
                downloadTask = DownloadPreviewAssetAsync(previewUrl, cachePath);
                _previewAssetTasks[previewUrl] = downloadTask;
            }
        }

        try
        {
            return await downloadTask;
        }
        finally
        {
            lock (_previewAssetSyncRoot)
            {
                if (_previewAssetTasks.TryGetValue(previewUrl, out var existingTask) && ReferenceEquals(existingTask, downloadTask))
                    _previewAssetTasks.Remove(previewUrl);
            }
        }
    }

    private async Task<string?> DownloadPreviewAssetAsync(string previewUrl, string cachePath)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            var tempPath = cachePath + ".download";
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            using var response = await PreviewAssetHttpClient.GetAsync(previewUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using (var sourceStream = await response.Content.ReadAsStreamAsync())
            using (var targetStream = File.Create(tempPath))
            {
                await sourceStream.CopyToAsync(targetStream);
            }

            if (File.Exists(cachePath))
                File.Delete(cachePath);

            File.Move(tempPath, cachePath);
            return cachePath;
        }
        catch
        {
            return null;
        }
    }

    private string? TryGetPreviewAssetCachePath(string previewUrl)
    {
        if (string.IsNullOrWhiteSpace(_previewCacheDirectory))
            return null;

        if (!Uri.TryCreate(previewUrl, UriKind.Absolute, out var previewUri))
            return null;

        var extension = Path.GetExtension(previewUri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".jpg";

        var normalizedExtension = extension.ToLowerInvariant();
        var fileName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(previewUrl))).ToLowerInvariant() + normalizedExtension;
        return Path.Combine(_previewCacheDirectory, fileName);
    }

    private int ResolvePreviewDecodeWidth()
    {
        var surfaceWidth = PreviewSurfaceBorder?.ActualWidth ?? 0;
        if (surfaceWidth <= 0)
            surfaceWidth = 520;

        var rasterizationScale = XamlRoot?.RasterizationScale ?? 1.0;
        var targetWidth = (int)Math.Ceiling(surfaceWidth * rasterizationScale);
        return Math.Max(320, Math.Min(720, targetWidth));
    }

    private void PreviewImageHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (PreviewImageFrame.Visibility == Visibility.Visible)
            UpdatePreviewFrameSize();
    }

    private void UpdatePreviewFrameMetrics(BitmapImage bitmap)
    {
        _previewPixelWidth = bitmap.PixelWidth;
        _previewPixelHeight = bitmap.PixelHeight;
        UpdatePreviewFrameSize();
    }

    private void UpdatePreviewFrameSize()
    {
        if (_previewPixelWidth <= 0 || _previewPixelHeight <= 0)
            return;

        var hostWidth = PreviewImageHost.ActualWidth;
        var hostHeight = PreviewImageHost.ActualHeight;
        if (hostWidth <= 0 || hostHeight <= 0)
            return;

        var scale = Math.Min(hostWidth / _previewPixelWidth, hostHeight / _previewPixelHeight);
        if (scale <= 0)
            return;

        PreviewImageFrame.Width = Math.Max(1, _previewPixelWidth * scale);
        PreviewImageFrame.Height = Math.Max(1, _previewPixelHeight * scale);
    }

    private void ResetPreviewFrame()
    {
        _previewPixelWidth = 0;
        _previewPixelHeight = 0;
        PreviewImageFrame.Width = double.NaN;
        PreviewImageFrame.Height = double.NaN;
    }

    private static HttpClient CreatePreviewAssetHttpClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
    }

    private void TrayStatus_Changed(object? sender, EventArgs e)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(RefreshTrayStatus);
            return;
        }

        RefreshTrayStatus();
    }

    private void RefreshTrayStatus()
    {
        if (_trayStatus == null)
        {
            TrayStatusText.Text = AppStrings.Get("Tray spike status is unavailable.");
            return;
        }

        TrayStatusText.Text = string.Join(Environment.NewLine, new[]
        {
            AppStrings.Format("Tray icon visible: {0}", _trayStatus.IsTrayIconVisible),
            AppStrings.Format("Window hidden to tray: {0}", _trayStatus.IsWindowHiddenToTray),
            AppStrings.Format("Hide count: {0}", _trayStatus.HideCount),
            AppStrings.Format("Restore count: {0}", _trayStatus.RestoreCount),
            AppStrings.Format("Last action: {0}", _trayStatus.LastAction),
            AppStrings.Format("Last backend check (UTC): {0}", _trayStatus.LastBackendCheckUtc.HasValue ? _trayStatus.LastBackendCheckUtc.Value.ToString("yyyy-MM-dd HH:mm:ss") : AppStrings.Get("not yet")),
        });
    }

    private void SetErrorState(string message)
    {
        StatusBar.Severity = InfoBarSeverity.Error;
        StatusBar.Title = AppStrings.Get("Backend startup failed");
        StatusBar.Message = message;
        MonthStatusBar.Severity = InfoBarSeverity.Error;
        MonthStatusBar.Title = AppStrings.Get("Calendar unavailable");
        MonthStatusBar.Message = message;
        SnapshotSummaryText.Text = message;
        var unavailable = AppStrings.Get("Unavailable");
        PreferredDateText.Text = unavailable;
        WallpaperStyleText.Text = unavailable;
        AutoCheckText.Text = unavailable;
        StartupText.Text = unavailable;
        ApiKeyStateText.Text = unavailable;
        ImagesDirectoryText.Text = unavailable;
        StorageModeText.Text = unavailable;
        LocalImageIndexText.Text = unavailable;
        TrayActionText.Text = unavailable;
        ResolvedDateText.Text = unavailable;
        PreviewWorkflowStatusText.Text = unavailable;
        PreviewSourceText.Text = unavailable;
        PreviewLocationText.Text = unavailable;
        PreviewMessageText.Text = message;
        PreviewPlaceholderTitleText.Text = AppStrings.Get("Preview unavailable");
        PreviewPlaceholderText.Text = message;
        PreviewPlaceholderIcon.Glyph = "\uE783";
        PreviewPlaceholderPanel.Visibility = Visibility.Visible;
        PreviewImageFrame.Visibility = Visibility.Collapsed;
        ResetPreviewFrame();
        PreviewProgressRing.IsActive = false;
        PreviewProgressRing.Visibility = Visibility.Collapsed;
        VisibleMonthText.Text = unavailable;
        SelectedDateText.Text = unavailable;
    }

    private static string FormatApiKeyState(apod_wallpaper.ApplicationInitialStateSnapshot snapshot)
    {
        var rawKey = snapshot.Settings != null ? snapshot.Settings.NasaApiKey : null;
        var usesDemoKey = string.IsNullOrWhiteSpace(rawKey) ||
            string.Equals(rawKey, "DEMO_KEY", StringComparison.OrdinalIgnoreCase);

        if (snapshot.ApiKeyValidationState == apod_wallpaper.ApiKeyValidationState.Valid && !usesDemoKey)
            return AppStrings.Get("Valid personal key");

        if (snapshot.ApiKeyValidationState == apod_wallpaper.ApiKeyValidationState.Invalid)
            return AppStrings.Get("Invalid key, using DEMO_KEY");

        return AppStrings.Get("DEMO_KEY / no personal key");
    }

    private bool UsesPersonalApiKey(apod_wallpaper.ApplicationInitialStateSnapshot snapshot)
    {
        var rawKey = snapshot.Settings?.NasaApiKey;
        if (string.IsNullOrWhiteSpace(rawKey))
            return false;

        if (string.Equals(rawKey, "DEMO_KEY", StringComparison.OrdinalIgnoreCase))
            return false;

        return snapshot.ApiKeyValidationState != apod_wallpaper.ApiKeyValidationState.Invalid;
    }

    private bool ShouldWarmMonth(DateTime month)
    {
        if (_initialStateSnapshot == null || !UsesPersonalApiKey(_initialStateSnapshot))
            return false;

        if (IsCurrentMonth(month))
            return true;

        return !_warmedMonths.Contains(month.Date);
    }

    private static bool IsCurrentMonth(DateTime month)
    {
        var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        return month.Date == currentMonth;
    }

    private string BuildMonthWarmupMessage(DateTime month)
    {
        if (IsCurrentMonth(month))
            return AppStrings.Get("Current month is refreshed in the background because APOD can still grow tomorrow or later today.");

        return AppStrings.Get("Background warmup is filling unknown dates and unsupported-media knowledge for this month.");
    }

    private static (int Local, int RemoteImage, int Unsupported, int Unknown) CountMonthStates(apod_wallpaper.ApodCalendarMonthState monthState)
    {
        var local = 0;
        var remoteImage = 0;
        var unsupported = 0;
        var unknown = 0;

        foreach (var day in monthState.Days)
        {
            if (day.IsFuture)
                continue;

            if (day.IsLocalImageAvailable)
                local++;
            else if (day.IsKnown && day.HasImage)
                remoteImage++;
            else if (day.IsKnown && !day.HasImage)
                unsupported++;
            else
                unknown++;
        }

        return (local, remoteImage, unsupported, unknown);
    }

    private static int GetMondayFirstOffset(DayOfWeek dayOfWeek)
    {
        return ((int)dayOfWeek + 6) % 7;
    }

    private static string BuildMiniStatusLabel(bool isFuture, bool hasLocalImage, bool hasRemoteImage, bool isUnsupported, bool isUnknown)
    {
        if (isFuture)
            return AppStrings.Get("future");
        if (hasLocalImage)
            return AppStrings.Get("local");
        if (hasRemoteImage)
            return AppStrings.Get("available");
        if (isUnsupported)
            return AppStrings.Get("video");
        if (isUnknown)
            return AppStrings.Get("unchecked");
        return string.Empty;
    }

    private string BuildDayTooltip(DateTime date, bool isFuture, bool hasLocalImage, bool hasRemoteImage, bool isUnsupported, bool isUnknown)
    {
        var status = isFuture
            ? AppStrings.Get("Future date")
            : hasLocalImage
                ? AppStrings.Get("Downloaded locally")
                : hasRemoteImage
                    ? AppStrings.Get("NASA image available")
                    : isUnsupported
                        ? AppStrings.Get("Video or unsupported content")
                        : AppStrings.Get("Unknown / not checked");

        var detail = isFuture
            ? AppStrings.Get("NASA has not published this date yet.")
            : hasLocalImage
                ? AppStrings.Get("A usable local wallpaper file exists on disk.")
                : hasRemoteImage
                    ? AppStrings.Get("This day resolves to image content, but the local file is not present.")
                    : isUnsupported
                        ? AppStrings.Get("The date was checked and does not contain a downloadable image.")
                        : UsesPersonalApiKey(_initialStateSnapshot!)
                            ? AppStrings.Get("The day is not verified yet or background month warmup has not reached it.")
                            : AppStrings.Get("Automatic month warmup is limited with DEMO_KEY to avoid spending the shared hourly quota.");

        return date.ToString("dddd, dd MMMM yyyy", AppStrings.DateCulture) + Environment.NewLine + status + Environment.NewLine + detail;
    }

    private static string ResolvePreviewBody(apod_wallpaper.ApodWorkflowResult workflow)
    {
        var explanation = ResolveOriginalExplanationText(workflow);
        if (!string.IsNullOrWhiteSpace(explanation))
            return explanation;

        return AppStrings.Get("Preview ready.");
    }

    private static string ResolveOriginalExplanationText(apod_wallpaper.ApodWorkflowResult workflow)
    {
        return workflow.Entry != null && !string.IsNullOrWhiteSpace(workflow.Entry.Explanation)
            ? workflow.Entry.Explanation.Trim()
            : string.Empty;
    }

    private static string ResolveUnavailablePreviewBody(apod_wallpaper.ApodWorkflowResult workflow)
    {
        return string.Empty;
    }

    private static string ResolvePlaceholderGlyph(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message) &&
            message.IndexOf("not published", StringComparison.OrdinalIgnoreCase) >= 0)
            return "\uE823";

        if (!string.IsNullOrWhiteSpace(message) &&
            (message.IndexOf("video", StringComparison.OrdinalIgnoreCase) >= 0 ||
             message.IndexOf("downloadable image", StringComparison.OrdinalIgnoreCase) >= 0))
            return "\uE714";

        return "\uE783";
    }

    private void EnsureCalendarGridDefinitions()
    {
        if (CalendarDaysGrid.ColumnDefinitions.Count == 7 && CalendarDaysGrid.RowDefinitions.Count == 6)
            return;

        CalendarDaysGrid.ColumnDefinitions.Clear();
        CalendarDaysGrid.RowDefinitions.Clear();

        for (var i = 0; i < 7; i++)
            CalendarDaysGrid.ColumnDefinitions.Add(new ColumnDefinition());

        for (var row = 0; row < 6; row++)
            CalendarDaysGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
    }

    private void EnsureCalendarMonthBuilt(DateTime month)
    {
        EnsureCalendarGridDefinitions();

        var normalizedMonth = new DateTime(month.Year, month.Month, 1);
        if (_renderedCalendarMonth == normalizedMonth)
            return;

        CalendarDaysGrid.Children.Clear();
        _calendarDayVisuals.Clear();
        _renderedCalendarMonth = normalizedMonth;

        var daysInMonth = DateTime.DaysInMonth(normalizedMonth.Year, normalizedMonth.Month);
        var startOffset = GetMondayFirstOffset(normalizedMonth.DayOfWeek);

        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(normalizedMonth.Year, normalizedMonth.Month, day);
            var cellIndex = startOffset + (day - 1);
            var row = cellIndex / 7;
            var column = cellIndex % 7;

            var visual = CreateCalendarDayVisual(date);
            _calendarDayVisuals[date.Date] = visual;

            Grid.SetRow(visual.Button, row);
            Grid.SetColumn(visual.Button, column);
            CalendarDaysGrid.Children.Add(visual.Button);
        }

        SetCalendarToLoadingState(normalizedMonth);
    }

    private void SetCalendarToLoadingState(DateTime month)
    {
        var latestPublishedDate = ResolveEffectiveLatestPublishedDate(GetLoadingLatestPublishedDate(month));
        foreach (var visual in _calendarDayVisuals.Values)
        {
            UpdateCalendarDayVisual(visual, visual.Date, dayState: null, latestPublishedDate, isLoading: true);
        }
    }

    private void UpdateCalendarSelectionOnly()
    {
        foreach (var visual in _calendarDayVisuals.Values)
        {
            var latestPublishedDate = ResolveEffectiveLatestPublishedDate(visual.LatestPublishedDate);
            if (!NeedsCalendarDayUpdate(visual, visual.CurrentDayState, latestPublishedDate, visual.IsLoading))
                continue;

            UpdateCalendarDayVisual(visual, visual.Date, visual.CurrentDayState, latestPublishedDate, visual.IsLoading);
        }
    }

    private DateTime ResolveEffectiveLatestPublishedDate(DateTime latestPublishedDate)
    {
        return apod_wallpaper.ApodCalendarAvailability.ResolveEffectiveLatestPublishedDate(latestPublishedDate, _transientAvailableApodDate);
    }

    private bool NeedsCalendarDayUpdate(
        CalendarDayVisual visual,
        apod_wallpaper.ApodCalendarDayState? dayState,
        DateTime latestPublishedDate,
        bool isLoading)
    {
        var targetSignature = BuildCalendarDaySignature(visual.Date, dayState, latestPublishedDate, isLoading);
        return !string.Equals(visual.LastVisualSignature, targetSignature, StringComparison.Ordinal);
    }

    private string BuildCalendarDaySignature(
        DateTime date,
        apod_wallpaper.ApodCalendarDayState? dayState,
        DateTime latestPublishedDate,
        bool isLoading)
    {
        var isFuture = date.Date > latestPublishedDate.Date;
        var hasLocalImage = dayState?.IsLocalImageAvailable == true;
        var hasRemoteImage = dayState?.IsKnown == true && dayState.HasImage && !hasLocalImage;
        var isUnsupported = dayState?.IsKnown == true && !dayState.HasImage;
        var isSelected = _selectedDate.Date == date.Date;

        return string.Join(
            "|",
            isFuture ? "future" : "active",
            hasLocalImage ? "local" : "no-local",
            hasRemoteImage ? "remote" : "no-remote",
            isUnsupported ? "unsupported" : "supported",
            isLoading ? "loading" : "ready",
            AppStrings.CurrentLanguage,
            isSelected ? "selected" : "idle",
            ActualTheme.ToString());
    }

    private static DateTime GetLoadingLatestPublishedDate(DateTime month)
    {
        var normalizedMonth = new DateTime(month.Year, month.Month, 1);
        var today = DateTime.Today;
        if (normalizedMonth.Year == today.Year && normalizedMonth.Month == today.Month)
            return today;

        return normalizedMonth < new DateTime(today.Year, today.Month, 1)
            ? new DateTime(normalizedMonth.Year, normalizedMonth.Month, DateTime.DaysInMonth(normalizedMonth.Year, normalizedMonth.Month))
            : normalizedMonth.AddDays(-1);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        if (_trayStatus != null)
            _trayStatus.Changed -= TrayStatus_Changed;

        base.OnNavigatedFrom(e);
    }

    private async Task RefreshSettingsSnapshotPreservingCalendarAsync()
    {
        if (_backendHost == null)
            return;

        var settingsResult = await _backendHost.Backend.GetSettingsAsync();
        if (!settingsResult.Succeeded || settingsResult.Value == null)
            return;

        _currentSettingsSnapshot = settingsResult.Value;
        AutoCheckText.Text = AppStrings.Get(_currentSettingsSnapshot.AutoRefreshEnabled ? "Enabled" : "Disabled");
        StartupText.Text = AppStrings.Get(_currentSettingsSnapshot.StartWithWindows ? "Enabled" : "Disabled");
        TrayActionText.Text = _currentSettingsSnapshot.TrayDoubleClickAction
            ? AppStrings.Get("Apply latest APOD")
            : AppStrings.Get("Default window action");
        SetAutoRefreshToggleState(_currentSettingsSnapshot.AutoRefreshEnabled);
        SetWallpaperStyleSelection(ResolveWallpaperStyleFromSettings(_currentSettingsSnapshot));
        UpdateActionAvailability();
    }

    private async Task<apod_wallpaper.ApplicationSettingsSnapshot> GetFreshSettingsSnapshotAsync()
    {
        if (_backendHost == null)
            return _currentSettingsSnapshot?.Clone() ?? new apod_wallpaper.ApplicationSettingsSnapshot();

        var latestSettingsResult = await _backendHost.Backend.GetSettingsAsync();
        if (latestSettingsResult.Succeeded && latestSettingsResult.Value != null)
            return latestSettingsResult.Value.Clone();

        return _currentSettingsSnapshot?.Clone() ?? new apod_wallpaper.ApplicationSettingsSnapshot();
    }
}
