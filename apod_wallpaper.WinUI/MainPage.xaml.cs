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
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;

namespace apod_wallpaper.WinUI;

public sealed partial class MainPage : Page
{
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

    private static readonly SolidColorBrush CalendarGreenBrush = new(ColorHelper.FromArgb(0xFF, 0x3D, 0x8C, 0x63));
    private static readonly SolidColorBrush CalendarBlueBrush = new(ColorHelper.FromArgb(0xFF, 0x2F, 0x79, 0xD9));
    private static readonly SolidColorBrush CalendarRedBrush = new(ColorHelper.FromArgb(0xFF, 0xC4, 0x5A, 0x5A));
    private static readonly SolidColorBrush CalendarUnknownBrush = new(ColorHelper.FromArgb(0xFF, 0x5F, 0x5F, 0x5F));
    private static readonly SolidColorBrush CalendarFutureBrush = new(ColorHelper.FromArgb(0xFF, 0x31, 0x31, 0x31));
    private static readonly SolidColorBrush CalendarSelectedBorderBrush = new(ColorHelper.FromArgb(0xFF, 0xF1, 0xF5, 0xF9));
    private static readonly SolidColorBrush CalendarDefaultForegroundBrush = new(Colors.White);
    private static readonly SolidColorBrush CalendarFutureForegroundBrush = new(ColorHelper.FromArgb(0xFF, 0xAA, 0xAA, 0xAA));
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

    public MainPage()
    {
        InitializeComponent();
        WallpaperStyleComboBox.ItemsSource = WallpaperStyleDisplayOrder;
        EnsureCalendarGridDefinitions();
        VisibleMonthText.Text = _visibleMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        RefreshSelectedDateText();
        EnsureCalendarMonthBuilt(_visibleMonth);
        UpdateActionAvailability();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        var arguments = e.Parameter as MainPageArguments;
        if (arguments == null)
        {
            SetErrorState("Main page did not receive backend composition root arguments.");
            return;
        }

        _backendHost = arguments.BackendHost;
        _initialization = arguments.Initialization;
        _trayStatus = arguments.TrayStatus;
        _trayStatus.Changed += TrayStatus_Changed;
        RefreshTrayStatus();

        if (_initialization == null || !_initialization.Succeeded)
        {
            SetErrorState(_initialization?.Error?.Message ?? "Backend initialization failed before the main page loaded.");
            return;
        }

        await RefreshStateAsync();
    }

    private async void ReloadPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadPreviewForSelectedDateAsync();
    }

    private async void DownloadOnlyButton_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteWallpaperActionAsync(
            "Downloading image",
            "Downloading APOD image for " + _selectedDate.ToString("yyyy-MM-dd") + ".",
            () => _backendHost!.Backend.DownloadDayAsync(_selectedDate));
    }

    private async void DownloadAndApplyButton_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteWallpaperActionAsync(
            "Applying wallpaper",
            "Downloading and applying APOD for " + _selectedDate.ToString("yyyy-MM-dd") + ".",
            () => _backendHost!.Backend.ApplyDayAsync(_selectedDate, GetSelectedWallpaperStyle()));
    }

    private async void ApplyLatestButton_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteWallpaperActionAsync(
            "Applying latest APOD",
            "Requesting the latest available APOD and applying it as wallpaper.",
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
            PreviewStatusBar.Title = "Unable to open NASA page";
            PreviewStatusBar.Message = postUrlResult.Error?.Message ?? "The backend did not return a valid APOD page URL.";
            return;
        }

        await Launcher.LaunchUriAsync(new Uri(postUrlResult.Value));
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
            SetErrorState("Backend host is not available.");
            return;
        }

        StatusBar.Severity = InfoBarSeverity.Informational;
        StatusBar.Title = "Loading backend state";
        StatusBar.Message = "Requesting the initial snapshot from apod_wallpaper.Core.";

        var snapshotResult = await _backendHost.GetInitialStateAsync();
        if (!snapshotResult.Succeeded || snapshotResult.Value == null)
        {
            SetErrorState(snapshotResult.Error?.Message ?? "Unable to load the initial snapshot.");
            return;
        }

        _initialStateSnapshot = snapshotResult.Value;
        _trayStatus?.MarkBackendCheck();
        ApplyInitialState(_initialStateSnapshot);

        StatusBar.Severity = InfoBarSeverity.Success;
        StatusBar.Title = "Backend initialized";
        StatusBar.Message = "The WinUI host created ApplicationController and loaded the initial snapshot in one backend call.";

        await Task.WhenAll(
            LoadVisibleMonthAsync(),
            LoadPreviewForSelectedDateAsync());
    }

    private void ApplyInitialState(apod_wallpaper.ApplicationInitialStateSnapshot snapshot)
    {
        _currentSettingsSnapshot = snapshot.Settings?.Clone() ?? new apod_wallpaper.ApplicationSettingsSnapshot();
        _lastPreviewWorkflow = null;
        var settings = _currentSettingsSnapshot;
        PreferredDateText.Text = snapshot.PreferredDisplayDate.ToString("yyyy-MM-dd");
        WallpaperStyleText.Text = snapshot.SelectedWallpaperStyle.ToString();
        AutoCheckText.Text = settings.AutoRefreshEnabled ? "Enabled" : "Disabled";
        StartupText.Text = settings.StartWithWindows ? "Enabled" : "Disabled";
        ApiKeyStateText.Text = FormatApiKeyState(snapshot);
        ImagesDirectoryText.Text = !string.IsNullOrWhiteSpace(snapshot.StoragePaths.ImagesDirectory)
            ? snapshot.StoragePaths.ImagesDirectory
            : "Not configured";
        StorageModeText.Text = snapshot.StoragePaths.Mode.ToString();
        LocalImageIndexText.Text = snapshot.LocalImageIndexReady ? "Ready" : "Not ready yet";
        TrayActionText.Text = settings.TrayDoubleClickAction
            ? "Apply latest APOD"
            : "Default window action";
        _previewCacheDirectory = Path.Combine(snapshot.StoragePaths.CacheDirectory, "previews");
        Directory.CreateDirectory(_previewCacheDirectory);

        _selectedDate = snapshot.PreferredDisplayDate.Date;
        _visibleMonth = new DateTime(_selectedDate.Year, _selectedDate.Month, 1);
        RefreshSelectedDateText();
        VisibleMonthText.Text = _visibleMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        EnsureCalendarMonthBuilt(_visibleMonth);
        UpdateCalendarSelectionOnly();
        SetWallpaperStyleSelection(snapshot.SelectedWallpaperStyle);
        UpdateActionAvailability();
        ActionStatusBar.Severity = InfoBarSeverity.Informational;
        ActionStatusBar.Title = "Actions ready";
        ActionStatusBar.Message = "Use Download, Download and apply, or Apply latest. Wallpaper style changes are saved through the backend and can reapply the current image.";

        SnapshotSummaryText.Text = string.Join(Environment.NewLine, new[]
        {
            "The calendar is the primary date selector now.",
            "Green is always resolved from the local images folder, so deleted files do not lie to the user.",
            "Red and blue states come from backend month knowledge persisted in metadata cache.",
            UsesPersonalApiKey(snapshot)
                ? "Personal API key detected. Month warmup will refresh missing dates in the background."
                : "DEMO_KEY mode detected. Automatic month warmup is limited to avoid burning the shared rate limit.",
        });
    }

    private async Task LoadVisibleMonthAsync()
    {
        if (_backendHost == null)
            return;

        var month = new DateTime(_visibleMonth.Year, _visibleMonth.Month, 1);
        var requestVersion = Interlocked.Increment(ref _monthRequestVersion);
        VisibleMonthText.Text = month.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
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
                MonthStatusBar.Title = "Refreshing month in background";
                MonthStatusBar.Message = BuildMonthWarmupMessage(month);
                _ = WarmMonthAsync(month, requestVersion);
            }

            return;
        }

        MonthStatusBar.Severity = InfoBarSeverity.Informational;
        MonthStatusBar.Title = "Loading cached month state";
        MonthStatusBar.Message = "Rendering cached knowledge first so the calendar appears immediately.";

        var cachedStopwatch = Stopwatch.StartNew();
        var cachedResult = await GetOrLoadMonthStateAsync(month, refreshMissingDates: false, apod_wallpaper.MonthRefreshMode.Balanced);
        cachedStopwatch.Stop();
        _lastMonthCachedElapsedMs = cachedStopwatch.ElapsedMilliseconds;
        if (requestVersion != _monthRequestVersion)
            return;

        if (!cachedResult.Succeeded || cachedResult.Value == null)
        {
            SetMonthErrorState(month, cachedResult.Error?.Message ?? "Unable to load calendar month state.");
            return;
        }

        _currentMonthState = cachedResult.Value;
        await ApplyCalendarMonthStateAsync(_currentMonthState, progressive: false, requestVersion);
        SetMonthReadyState(_currentMonthState, warmed: false);

        if (!ShouldWarmMonth(month))
            return;

        MonthStatusBar.Severity = InfoBarSeverity.Informational;
        MonthStatusBar.Title = "Refreshing month in background";
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
            MonthStatusBar.Title = "Month refresh partially unavailable";
            MonthStatusBar.Message = refreshedResult.Error?.Message ?? "The calendar kept its cached state because background refresh did not finish cleanly.";
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
        MonthStatusBar.Title = warmed ? "Month refreshed" : "Month loaded";
        MonthStatusBar.Message = string.Format(
            CultureInfo.InvariantCulture,
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
        VisibleMonthText.Text = month.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        MonthStatusBar.Severity = InfoBarSeverity.Error;
        MonthStatusBar.Title = "Calendar month failed";
        MonthStatusBar.Message = message;
        SelectedDateText.Text = "Calendar unavailable";
    }

    private async Task ApplyCalendarMonthStateAsync(apod_wallpaper.ApodCalendarMonthState monthState, bool progressive, int requestVersion)
    {
        EnsureCalendarGridDefinitions();
        EnsureCalendarMonthBuilt(monthState.Month);

        VisibleMonthText.Text = monthState.Month.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        RefreshSelectedDateText();

        var monthStart = monthState.Month;
        var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
        var changedVisuals = new List<(CalendarDayVisual Visual, apod_wallpaper.ApodCalendarDayState? DayState)>();

        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(monthStart.Year, monthStart.Month, day);
            monthState.TryGetDay(date, out var dayState);

            if (_calendarDayVisuals.TryGetValue(date.Date, out var visual))
            {
                if (NeedsCalendarDayUpdate(visual, dayState, monthState.LatestPublishedDate, isLoading: false))
                    changedVisuals.Add((visual, dayState));
            }
        }

        if (!progressive || changedVisuals.Count <= 1)
        {
            foreach (var changedVisual in changedVisuals)
                UpdateCalendarDayVisual(changedVisual.Visual, changedVisual.Visual.Date, changedVisual.DayState, monthState.LatestPublishedDate, isLoading: false);

            return;
        }

        foreach (var changedVisual in changedVisuals)
        {
            if (requestVersion != _monthRequestVersion)
                return;

            UpdateCalendarDayVisual(changedVisual.Visual, changedVisual.Visual.Date, changedVisual.DayState, monthState.LatestPublishedDate, isLoading: false);
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
            FontSize = 10,
            Opacity = 0.92,
        };

        var content = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
        };

        content.Children.Add(dayNumberText);
        content.Children.Add(statusText);

        var button = new Button
        {
            Content = content,
            Padding = new Thickness(6),
            MinHeight = 58,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            CornerRadius = new CornerRadius(12),
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

        var background = CalendarUnknownBrush;
        var foreground = CalendarDefaultForegroundBrush;
        if (isFuture)
        {
            background = CalendarFutureBrush;
            foreground = CalendarFutureForegroundBrush;
        }
        else if (hasLocalImage)
        {
            background = CalendarGreenBrush;
        }
        else if (hasRemoteImage)
        {
            background = CalendarBlueBrush;
        }
        else if (isUnsupported)
        {
            background = CalendarRedBrush;
        }

        visual.DayNumberText.Text = date.Day.ToString(CultureInfo.InvariantCulture);
        visual.DayNumberText.FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal;
        visual.StatusText.Text = isLoading
            ? (isFuture ? "future" : "loading")
            : BuildMiniStatusLabel(isFuture, hasLocalImage, hasRemoteImage, isUnsupported, isUnknown);

        visual.Button.Background = background;
        visual.Button.Foreground = foreground;
        visual.Button.BorderBrush = isSelected ? CalendarSelectedBorderBrush : background;
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
                ? date.ToString("dddd, dd MMMM yyyy", CultureInfo.CurrentCulture) + Environment.NewLine + "Loading calendar state"
                : BuildDayTooltip(date, isFuture, hasLocalImage, hasRemoteImage, isUnsupported, isUnknown));
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
        PreviewStatusBar.Title = "Loading preview";
        PreviewStatusBar.Message = "Loading APOD preview for " + selectedDate.ToString("yyyy-MM-dd") + ".";

        RequestedDateText.Text = selectedDate.ToString("yyyy-MM-dd");
        ResolvedDateText.Text = "Loading...";
        PreviewWorkflowStatusText.Text = "Loading";
        PreviewSourceText.Text = "Resolving...";
        PreviewLocationText.Text = "Resolving preview location...";
        PreviewMessageText.Text = "The UI will ignore stale results if another date is requested before this one finishes.";

        PreviewImage.Source = null;
        PreviewImage.Visibility = Visibility.Collapsed;
        PreviewPlaceholderPanel.Visibility = Visibility.Collapsed;
        PreviewProgressRing.IsActive = true;
        PreviewProgressRing.Visibility = Visibility.Visible;
    }

    private async Task SetPreviewSuccessAsync(apod_wallpaper.ApodWorkflowResult workflow)
    {
        PreviewStatusBar.Severity = InfoBarSeverity.Success;
        PreviewStatusBar.Title = "Preview loaded";
        PreviewStatusBar.Message = string.Format(
            CultureInfo.InvariantCulture,
            "{0} Backend: {1} ms. Rendering preview image...",
            workflow.Message ?? "Preview metadata loaded successfully.",
            _lastPreviewBackendElapsedMs);

        RequestedDateText.Text = workflow.RequestedDate.ToString("yyyy-MM-dd");
        ResolvedDateText.Text = workflow.ResolvedDate.HasValue
            ? workflow.ResolvedDate.Value.ToString("yyyy-MM-dd")
            : workflow.RequestedDate.ToString("yyyy-MM-dd");
        PreviewWorkflowStatusText.Text = workflow.Status.ToString();
        PreviewSourceText.Text = workflow.Source.ToString();
        PreviewLocationText.Text = workflow.PreviewLocation ?? "No preview location";
        PreviewMessageText.Text = string.IsNullOrWhiteSpace(workflow.Message)
            ? "Success"
            : workflow.Message;
        UpdateActionAvailability();

        var imageLoaded = await TryShowPreviewImageAsync(workflow.PreviewLocation);
        PreviewProgressRing.IsActive = false;
        PreviewProgressRing.Visibility = Visibility.Collapsed;

        if (!imageLoaded)
        {
            PreviewPlaceholderTitleText.Text = "Preview image unavailable";
            PreviewPlaceholderText.Text = "The backend returned a successful workflow, but the preview image could not be opened by the host.";
            PreviewPlaceholderPanel.Visibility = Visibility.Visible;
            PreviewStatusBar.Severity = InfoBarSeverity.Warning;
            PreviewStatusBar.Title = "Preview metadata loaded";
            PreviewStatusBar.Message = "Workflow succeeded, but the image did not render in the host.";
        }
    }

    private void SetPreviewUnavailable(apod_wallpaper.ApodWorkflowResult workflow)
    {
        PreviewProgressRing.IsActive = false;
        PreviewProgressRing.Visibility = Visibility.Collapsed;
        PreviewImage.Source = null;
        PreviewImage.Visibility = Visibility.Collapsed;
        PreviewPlaceholderPanel.Visibility = Visibility.Visible;

        PreviewStatusBar.Severity = InfoBarSeverity.Warning;
        PreviewStatusBar.Title = "Preview unavailable";
        PreviewStatusBar.Message = workflow.Message ?? "The selected APOD date is currently unavailable.";

        RequestedDateText.Text = workflow.RequestedDate.ToString("yyyy-MM-dd");
        ResolvedDateText.Text = workflow.ResolvedDate.HasValue
            ? workflow.ResolvedDate.Value.ToString("yyyy-MM-dd")
            : "Unavailable";
        PreviewWorkflowStatusText.Text = workflow.Status.ToString();
        PreviewSourceText.Text = workflow.Source.ToString();
        PreviewLocationText.Text = "No preview image";
        PreviewMessageText.Text = workflow.Message ?? "Unavailable";
        UpdateActionAvailability();

        PreviewPlaceholderTitleText.Text = "No image preview";
        PreviewPlaceholderText.Text = workflow.Message ?? "The selected date does not currently resolve to previewable image content.";
    }

    private void SetPreviewOperationError(DateTime selectedDate, string message)
    {
        PreviewProgressRing.IsActive = false;
        PreviewProgressRing.Visibility = Visibility.Collapsed;
        PreviewImage.Source = null;
        PreviewImage.Visibility = Visibility.Collapsed;
        PreviewPlaceholderPanel.Visibility = Visibility.Visible;

        PreviewStatusBar.Severity = InfoBarSeverity.Error;
        PreviewStatusBar.Title = "Preview failed";
        PreviewStatusBar.Message = message;

        RequestedDateText.Text = selectedDate.ToString("yyyy-MM-dd");
        ResolvedDateText.Text = "Error";
        PreviewWorkflowStatusText.Text = "Error";
        PreviewSourceText.Text = "Unknown";
        PreviewLocationText.Text = "No preview image";
        PreviewMessageText.Text = message;
        UpdateActionAvailability();

        PreviewPlaceholderTitleText.Text = "Preview failed";
        PreviewPlaceholderText.Text = message;
    }

    private async void WallpaperStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressWallpaperStyleSelectionChanged || _backendHost == null || _currentSettingsSnapshot == null)
            return;

        var selectedStyle = GetSelectedWallpaperStyle();
        if (_currentSettingsSnapshot.WallpaperStyleIndex == (int)selectedStyle)
            return;

        var updatedSettings = _currentSettingsSnapshot.Clone();
        updatedSettings.WallpaperStyleIndex = (int)selectedStyle;

        ActionStatusBar.Severity = InfoBarSeverity.Informational;
        ActionStatusBar.Title = "Saving wallpaper style";
        ActionStatusBar.Message = "Persisting wallpaper style through the backend facade.";

        var saveResult = await _backendHost.Backend.SaveSettingsAsync(updatedSettings);
        if (!saveResult.Succeeded || saveResult.Value == null)
        {
            ActionStatusBar.Severity = InfoBarSeverity.Error;
            ActionStatusBar.Title = "Wallpaper style was not saved";
            ActionStatusBar.Message = saveResult.Error?.Message ?? "Unable to persist wallpaper style.";
            SetWallpaperStyleSelection(ResolveWallpaperStyleFromSettings(_currentSettingsSnapshot));
            return;
        }

        _currentSettingsSnapshot = saveResult.Value;
        WallpaperStyleText.Text = selectedStyle.ToString();
        if (_initialStateSnapshot != null)
        {
            _initialStateSnapshot.Settings = saveResult.Value.Clone();
            _initialStateSnapshot.SelectedWallpaperStyle = selectedStyle;
        }

        ActionStatusBar.Severity = InfoBarSeverity.Success;
        ActionStatusBar.Title = "Wallpaper style saved";
        ActionStatusBar.Message = "Future apply actions will use " + selectedStyle + ".";

        if (_lastPreviewWorkflow?.Status == apod_wallpaper.ApodWorkflowStatus.Success && _lastPreviewWorkflow.Entry?.HasImage == true)
        {
            await ExecuteWallpaperActionAsync(
                "Reapplying current image",
                "Wallpaper style changed to " + selectedStyle + ". Reapplying the selected image.",
                () => _backendHost.Backend.ApplyDayAsync(_selectedDate, selectedStyle),
                updateSelectionFromWorkflow: false);
        }
    }

    private async Task<bool> TryShowPreviewImageAsync(string? previewLocation)
    {
        PreviewImage.Source = null;
        PreviewImage.Visibility = Visibility.Collapsed;
        PreviewPlaceholderPanel.Visibility = Visibility.Collapsed;
        _pendingPreviewLocation = null;
        _previewImageStopwatch.Reset();

        if (string.IsNullOrWhiteSpace(previewLocation))
            return false;

        try
        {
            var resolvedPreviewLocation = await ResolvePreviewAssetLocationAsync(previewLocation);
            var bitmap = new BitmapImage();
            bitmap.DecodePixelWidth = ResolvePreviewDecodeWidth();
            _pendingPreviewLocation = resolvedPreviewLocation;
            _previewImageStopwatch.Restart();
            var previewUri = BuildPreviewUri(resolvedPreviewLocation);

            if (previewUri.IsFile)
            {
                var previewFile = await StorageFile.GetFileFromPathAsync(previewUri.LocalPath);
                using IRandomAccessStream stream = await previewFile.OpenReadAsync();
                await bitmap.SetSourceAsync(stream);
                PreviewImage.Source = bitmap;
                CompletePreviewImageRender();
            }
            else
            {
                bitmap.UriSource = previewUri;
                PreviewImage.Source = bitmap;
            }

            PreviewImage.Visibility = Visibility.Visible;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void RefreshSelectedDateText()
    {
        SelectedDateText.Text = "Selected date: " + _selectedDate.ToString("dddd, dd MMMM yyyy", CultureInfo.CurrentCulture);
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

    private void UpdateActionAvailability()
    {
        var hasBackend = _backendHost != null;
        var hasSuccessfulPreview = _lastPreviewWorkflow?.Status == apod_wallpaper.ApodWorkflowStatus.Success;

        ReloadPreviewButton.IsEnabled = hasBackend && !_isApplyingWallpaperAction;
        OpenNasaPageButton.IsEnabled = hasBackend && !_isApplyingWallpaperAction;
        WallpaperStyleComboBox.IsEnabled = hasBackend && !_isApplyingWallpaperAction;
        DownloadOnlyButton.IsEnabled = hasBackend && !_isApplyingWallpaperAction;
        DownloadAndApplyButton.IsEnabled = hasBackend && !_isApplyingWallpaperAction;
        ApplyLatestButton.IsEnabled = hasBackend && !_isApplyingWallpaperAction;

        if (!hasSuccessfulPreview && !_isApplyingWallpaperAction)
            WallpaperStyleComboBox.IsEnabled = hasBackend && _currentSettingsSnapshot != null;
    }

    private async Task ExecuteWallpaperActionAsync(
        string title,
        string message,
        Func<Task<apod_wallpaper.OperationResult<apod_wallpaper.ApodWorkflowResult>>> action,
        bool updateSelectionFromWorkflow = true)
    {
        if (_backendHost == null || _isApplyingWallpaperAction)
            return;

        _isApplyingWallpaperAction = true;
        UpdateActionAvailability();
        ActionProgressRing.IsActive = true;
        ActionProgressRing.Visibility = Visibility.Visible;
        ActionStatusBar.Severity = InfoBarSeverity.Informational;
        ActionStatusBar.Title = title;
        ActionStatusBar.Message = message;

        try
        {
            var operationResult = await action();
            if (!operationResult.Succeeded || operationResult.Value == null)
            {
                ActionStatusBar.Severity = InfoBarSeverity.Error;
                ActionStatusBar.Title = title + " failed";
                ActionStatusBar.Message = operationResult.Error?.Message ?? "The backend did not complete the requested action.";
                return;
            }

            var workflow = operationResult.Value;
            _lastPreviewWorkflow = workflow;

            if (workflow.Status == apod_wallpaper.ApodWorkflowStatus.Unavailable)
            {
                ActionStatusBar.Severity = InfoBarSeverity.Warning;
                ActionStatusBar.Title = title + " unavailable";
                ActionStatusBar.Message = workflow.Message ?? "The selected APOD entry does not contain downloadable image content.";
                SetPreviewUnavailable(workflow);
                return;
            }

            if (updateSelectionFromWorkflow && workflow.ResolvedDate.HasValue)
            {
                _selectedDate = workflow.ResolvedDate.Value.Date;
                RefreshSelectedDateText();
                UpdateCalendarSelectionOnly();
            }

            await SetPreviewSuccessAsync(workflow);
            await RefreshMonthStateAfterWorkflowAsync(workflow);

            ActionStatusBar.Severity = InfoBarSeverity.Success;
            ActionStatusBar.Title = title + " complete";
            ActionStatusBar.Message = BuildActionSuccessMessage(workflow);
        }
        finally
        {
            ActionProgressRing.IsActive = false;
            ActionProgressRing.Visibility = Visibility.Collapsed;
            _isApplyingWallpaperAction = false;
            UpdateActionAvailability();
        }
    }

    private async Task RefreshMonthStateAfterWorkflowAsync(apod_wallpaper.ApodWorkflowResult workflow)
    {
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

    private void PreviewImage_ImageOpened(object sender, RoutedEventArgs e)
    {
        CompletePreviewImageRender();
    }

    private void CompletePreviewImageRender()
    {
        if (!_previewImageStopwatch.IsRunning)
            return;

        _previewImageStopwatch.Stop();
        PreviewStatusBar.Severity = InfoBarSeverity.Success;
        PreviewStatusBar.Title = "Preview rendered";
        PreviewStatusBar.Message = string.Format(
            CultureInfo.InvariantCulture,
            "Backend: {0} ms, image render: {1} ms.",
            _lastPreviewBackendElapsedMs,
            _previewImageStopwatch.ElapsedMilliseconds);
    }

    private void PreviewImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        if (_previewImageStopwatch.IsRunning)
            _previewImageStopwatch.Stop();

        PreviewStatusBar.Severity = InfoBarSeverity.Warning;
        PreviewStatusBar.Title = "Preview metadata loaded";
        PreviewStatusBar.Message = string.Format(
            CultureInfo.InvariantCulture,
            "Backend: {0} ms. The preview image failed to render in the host.",
            _lastPreviewBackendElapsedMs);

        PreviewPlaceholderTitleText.Text = "Preview image unavailable";
        PreviewPlaceholderText.Text = "The backend resolved preview metadata, but WinUI could not render the image from " + (_pendingPreviewLocation ?? "the resolved source") + ".";
        PreviewPlaceholderPanel.Visibility = Visibility.Visible;
        PreviewImage.Visibility = Visibility.Collapsed;
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
            TrayStatusText.Text = "Tray spike status is unavailable.";
            return;
        }

        TrayStatusText.Text = string.Join(Environment.NewLine, new[]
        {
            "Tray icon visible: " + _trayStatus.IsTrayIconVisible,
            "Window hidden to tray: " + _trayStatus.IsWindowHiddenToTray,
            "Hide count: " + _trayStatus.HideCount,
            "Restore count: " + _trayStatus.RestoreCount,
            "Last action: " + _trayStatus.LastAction,
            "Last backend check (UTC): " + (_trayStatus.LastBackendCheckUtc.HasValue ? _trayStatus.LastBackendCheckUtc.Value.ToString("yyyy-MM-dd HH:mm:ss") : "not yet"),
        });
    }

    private void SetErrorState(string message)
    {
        StatusBar.Severity = InfoBarSeverity.Error;
        StatusBar.Title = "Backend startup failed";
        StatusBar.Message = message;
        MonthStatusBar.Severity = InfoBarSeverity.Error;
        MonthStatusBar.Title = "Calendar unavailable";
        MonthStatusBar.Message = message;
        SnapshotSummaryText.Text = message;
        PreferredDateText.Text = "Unavailable";
        WallpaperStyleText.Text = "Unavailable";
        AutoCheckText.Text = "Unavailable";
        StartupText.Text = "Unavailable";
        ApiKeyStateText.Text = "Unavailable";
        ImagesDirectoryText.Text = "Unavailable";
        StorageModeText.Text = "Unavailable";
        LocalImageIndexText.Text = "Unavailable";
        TrayActionText.Text = "Unavailable";
        RequestedDateText.Text = "Unavailable";
        ResolvedDateText.Text = "Unavailable";
        PreviewWorkflowStatusText.Text = "Unavailable";
        PreviewSourceText.Text = "Unavailable";
        PreviewLocationText.Text = "Unavailable";
        PreviewMessageText.Text = message;
        PreviewPlaceholderTitleText.Text = "Preview unavailable";
        PreviewPlaceholderText.Text = message;
        PreviewPlaceholderPanel.Visibility = Visibility.Visible;
        PreviewImage.Visibility = Visibility.Collapsed;
        PreviewProgressRing.IsActive = false;
        PreviewProgressRing.Visibility = Visibility.Collapsed;
        VisibleMonthText.Text = "Unavailable";
        SelectedDateText.Text = "Unavailable";
    }

    private static string FormatApiKeyState(apod_wallpaper.ApplicationInitialStateSnapshot snapshot)
    {
        var rawKey = snapshot.Settings != null ? snapshot.Settings.NasaApiKey : null;
        var usesDemoKey = string.IsNullOrWhiteSpace(rawKey) ||
            string.Equals(rawKey, "DEMO_KEY", StringComparison.OrdinalIgnoreCase);

        if (snapshot.ApiKeyValidationState == apod_wallpaper.ApiKeyValidationState.Valid && !usesDemoKey)
            return "Valid personal key";

        if (snapshot.ApiKeyValidationState == apod_wallpaper.ApiKeyValidationState.Invalid)
            return "Invalid key, using DEMO_KEY";

        return "DEMO_KEY / no personal key";
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
            return "Current month is refreshed in the background because APOD can still grow tomorrow or later today.";

        return "Background warmup is filling unknown dates and unsupported-media knowledge for this month.";
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
            return "future";
        if (hasLocalImage)
            return "local";
        if (hasRemoteImage)
            return "ready";
        if (isUnsupported)
            return "video";
        if (isUnknown)
            return "unknown";
        return string.Empty;
    }

    private string BuildDayTooltip(DateTime date, bool isFuture, bool hasLocalImage, bool hasRemoteImage, bool isUnsupported, bool isUnknown)
    {
        var status = isFuture
            ? "Future date"
            : hasLocalImage
                ? "Downloaded locally"
                : hasRemoteImage
                    ? "NASA image available"
                    : isUnsupported
                        ? "Video or unsupported content"
                        : "Unknown / not checked";

        var detail = isFuture
            ? "NASA has not published this date yet."
            : hasLocalImage
                ? "A usable local wallpaper file exists on disk."
                : hasRemoteImage
                    ? "This day resolves to image content, but the local file is not present."
                    : isUnsupported
                        ? "The date was checked and does not contain a downloadable image."
                        : UsesPersonalApiKey(_initialStateSnapshot!)
                            ? "The day is not verified yet or background month warmup has not reached it."
                            : "Automatic month warmup is limited with DEMO_KEY to avoid spending the shared hourly quota.";

        return date.ToString("dddd, dd MMMM yyyy", CultureInfo.CurrentCulture) + Environment.NewLine + status + Environment.NewLine + detail;
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
        var latestPublishedDate = GetLoadingLatestPublishedDate(month);
        foreach (var visual in _calendarDayVisuals.Values)
        {
            UpdateCalendarDayVisual(visual, visual.Date, dayState: null, latestPublishedDate, isLoading: true);
        }
    }

    private void UpdateCalendarSelectionOnly()
    {
        foreach (var visual in _calendarDayVisuals.Values)
        {
            if (!NeedsCalendarDayUpdate(visual, visual.CurrentDayState, visual.LatestPublishedDate, visual.IsLoading))
                continue;

            UpdateCalendarDayVisual(visual, visual.Date, visual.CurrentDayState, visual.LatestPublishedDate, visual.IsLoading);
        }
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
            isSelected ? "selected" : "idle");
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
}
