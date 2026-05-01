using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace apod_wallpaper.WinUI;

public sealed partial class MainPage : Page
{
    private sealed class CalendarDayVisual
    {
        public required DateTime Date { get; init; }
        public required Button Button { get; init; }
        public required TextBlock DayNumberText { get; init; }
        public required TextBlock StatusText { get; init; }
        public bool IsLoading { get; set; }
    }

    private static readonly SolidColorBrush CalendarGreenBrush = new(ColorHelper.FromArgb(0xFF, 0x3D, 0x8C, 0x63));
    private static readonly SolidColorBrush CalendarBlueBrush = new(ColorHelper.FromArgb(0xFF, 0x2F, 0x79, 0xD9));
    private static readonly SolidColorBrush CalendarRedBrush = new(ColorHelper.FromArgb(0xFF, 0xC4, 0x5A, 0x5A));
    private static readonly SolidColorBrush CalendarUnknownBrush = new(ColorHelper.FromArgb(0xFF, 0x5F, 0x5F, 0x5F));
    private static readonly SolidColorBrush CalendarFutureBrush = new(ColorHelper.FromArgb(0xFF, 0x31, 0x31, 0x31));
    private static readonly SolidColorBrush CalendarSelectedBorderBrush = new(ColorHelper.FromArgb(0xFF, 0xF1, 0xF5, 0xF9));
    private static readonly SolidColorBrush CalendarDefaultForegroundBrush = new(Colors.White);
    private static readonly SolidColorBrush CalendarFutureForegroundBrush = new(ColorHelper.FromArgb(0xFF, 0xAA, 0xAA, 0xAA));

    private BackendHost? _backendHost;
    private apod_wallpaper.OperationResult<apod_wallpaper.ApplicationSettingsSnapshot>? _initialization;
    private TraySpikeStatus? _trayStatus;
    private apod_wallpaper.ApplicationInitialStateSnapshot? _initialStateSnapshot;
    private apod_wallpaper.ApodCalendarMonthState? _currentMonthState;
    private int _previewRequestVersion;
    private int _monthRequestVersion;
    private readonly HashSet<DateTime> _warmedMonths = new();
    private readonly Dictionary<DateTime, CalendarDayVisual> _calendarDayVisuals = new();
    private DateTime _selectedDate = DateTime.Today;
    private DateTime _visibleMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime? _renderedCalendarMonth;
    private readonly Stopwatch _previewImageStopwatch = new();
    private long _lastPreviewBackendElapsedMs;
    private long _lastMonthCachedElapsedMs;
    private string? _pendingPreviewLocation;

    public MainPage()
    {
        InitializeComponent();
        EnsureCalendarGridDefinitions();
        VisibleMonthText.Text = _visibleMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        RefreshSelectedDateText();
        EnsureCalendarMonthBuilt(_visibleMonth);
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
        PreferredDateText.Text = snapshot.PreferredDisplayDate.ToString("yyyy-MM-dd");
        WallpaperStyleText.Text = snapshot.SelectedWallpaperStyle.ToString();
        AutoCheckText.Text = snapshot.Settings.AutoRefreshEnabled ? "Enabled" : "Disabled";
        StartupText.Text = snapshot.Settings.StartWithWindows ? "Enabled" : "Disabled";
        ApiKeyStateText.Text = FormatApiKeyState(snapshot);
        ImagesDirectoryText.Text = !string.IsNullOrWhiteSpace(snapshot.StoragePaths.ImagesDirectory)
            ? snapshot.StoragePaths.ImagesDirectory
            : "Not configured";
        StorageModeText.Text = snapshot.StoragePaths.Mode.ToString();
        LocalImageIndexText.Text = snapshot.LocalImageIndexReady ? "Ready" : "Not ready yet";
        TrayActionText.Text = snapshot.Settings.TrayDoubleClickAction
            ? "Apply latest APOD"
            : "Default window action";

        _selectedDate = snapshot.PreferredDisplayDate.Date;
        _visibleMonth = new DateTime(_selectedDate.Year, _selectedDate.Month, 1);
        RefreshSelectedDateText();
        VisibleMonthText.Text = _visibleMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        EnsureCalendarMonthBuilt(_visibleMonth);
        UpdateCalendarSelectionOnly();

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

        MonthStatusBar.Severity = InfoBarSeverity.Informational;
        MonthStatusBar.Title = "Loading cached month state";
        MonthStatusBar.Message = "Rendering cached knowledge first so the calendar appears immediately.";

        var cachedStopwatch = Stopwatch.StartNew();
        var cachedResult = await _backendHost.Backend.GetCalendarMonthStateAsync(month, false, apod_wallpaper.MonthRefreshMode.Balanced);
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
        RenderCalendarMonth(_currentMonthState);
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
        RenderCalendarMonth(_currentMonthState);
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

    private void RenderCalendarMonth(apod_wallpaper.ApodCalendarMonthState monthState)
    {
        EnsureCalendarGridDefinitions();
        EnsureCalendarMonthBuilt(monthState.Month);

        VisibleMonthText.Text = monthState.Month.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        RefreshSelectedDateText();

        var monthStart = monthState.Month;
        var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);

        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(monthStart.Year, monthStart.Month, day);
            monthState.TryGetDay(date, out var dayState);

            if (_calendarDayVisuals.TryGetValue(date.Date, out var visual))
                UpdateCalendarDayVisual(visual, date, dayState, monthState.LatestPublishedDate, isLoading: false);
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
            RenderCalendarMonth(_currentMonthState);
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
            SetPreviewOperationError(selectedDate, operationResult.Error?.Message ?? "Unable to load preview.");
            return;
        }

        var workflow = operationResult.Value;
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
        var refreshedResult = await _backendHost.Backend.GetCalendarMonthStateAsync(month, true, apod_wallpaper.MonthRefreshMode.Balanced);
        if (!refreshedResult.Succeeded || refreshedResult.Value == null)
            return;

        _currentMonthState = refreshedResult.Value;
        RenderCalendarMonth(_currentMonthState);
        SetMonthReadyState(_currentMonthState, warmed: false);
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

        PreviewPlaceholderTitleText.Text = "Preview failed";
        PreviewPlaceholderText.Text = message;
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
            var bitmap = new BitmapImage();
            bitmap.DecodePixelWidth = 960;
            _pendingPreviewLocation = previewLocation;
            _previewImageStopwatch.Restart();
            bitmap.UriSource = BuildPreviewUri(previewLocation);
            PreviewImage.Source = bitmap;
            PreviewImage.Visibility = Visibility.Visible;
            await Task.CompletedTask;
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

    private void PreviewImage_ImageOpened(object sender, RoutedEventArgs e)
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
        var latestPublishedDate = _currentMonthState?.LatestPublishedDate ?? GetLoadingLatestPublishedDate(_visibleMonth);
        foreach (var visual in _calendarDayVisuals.Values)
        {
            UpdateCalendarDayVisual(
                visual,
                visual.Date,
                dayState: null,
                latestPublishedDate,
                isLoading: visual.IsLoading);
        }
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
