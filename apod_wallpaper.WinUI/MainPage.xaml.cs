using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace apod_wallpaper.WinUI;

public sealed partial class MainPage : Page
{
    private BackendHost? _backendHost;
    private apod_wallpaper.OperationResult<apod_wallpaper.ApplicationSettingsSnapshot>? _initialization;
    private TraySpikeStatus? _trayStatus;
    private Action? _hideWindowToTray;
    private Func<Task>? _exitApplicationAsync;
    private apod_wallpaper.ApplicationInitialStateSnapshot? _initialStateSnapshot;
    private int _previewRequestVersion;
    private bool _suppressDateChanged;

    public MainPage()
    {
        InitializeComponent();
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
        _hideWindowToTray = arguments.HideWindowToTray;
        _exitApplicationAsync = arguments.ExitApplicationAsync;
        _trayStatus.Changed += TrayStatus_Changed;
        RefreshTrayStatus();

        if (_initialization == null || !_initialization.Succeeded)
        {
            SetErrorState(_initialization?.Error?.Message ?? "Backend initialization failed before the main page loaded.");
            return;
        }

        await RefreshStateAndPreviewAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshStateAndPreviewAsync();
    }

    private async void ReloadPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadPreviewForSelectedDateAsync();
    }

    private async void OpenNasaPageButton_Click(object sender, RoutedEventArgs e)
    {
        if (_backendHost == null)
            return;

        var selectedDate = PreviewDatePicker.Date.Date;
        var postUrlResult = await _backendHost.Backend.GetPostUrlAsync(selectedDate);
        if (!postUrlResult.Succeeded || string.IsNullOrWhiteSpace(postUrlResult.Value))
        {
            PreviewStatusBar.Severity = InfoBarSeverity.Error;
            PreviewStatusBar.Title = "Unable to open NASA page";
            PreviewStatusBar.Message = postUrlResult.Error?.Message ?? "The backend did not return a valid APOD page URL.";
            return;
        }

        await Launcher.LaunchUriAsync(new Uri(postUrlResult.Value));
    }

    private async void PreviewDatePicker_DateChanged(object sender, DatePickerValueChangedEventArgs args)
    {
        if (_suppressDateChanged)
            return;

        await LoadPreviewForSelectedDateAsync();
    }

    private void HideToTrayButton_Click(object sender, RoutedEventArgs e)
    {
        _hideWindowToTray?.Invoke();
    }

    private async void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        if (_exitApplicationAsync != null)
            await _exitApplicationAsync();
    }

    private async Task RefreshStateAndPreviewAsync()
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

        await LoadPreviewForSelectedDateAsync();
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

        _suppressDateChanged = true;
        PreviewDatePicker.Date = new DateTimeOffset(snapshot.PreferredDisplayDate);
        _suppressDateChanged = false;

        SnapshotSummaryText.Text = string.Join(Environment.NewLine, new[]
        {
            "One backend call returned both current state and persisted settings.",
            "This screen is intentionally readonly for now.",
            "Preview requests use stale-result protection so quick date changes do not overwrite newer UI state.",
            "Settings file: " + snapshot.StoragePaths.SettingsFilePath,
        });
    }

    private async Task LoadPreviewForSelectedDateAsync()
    {
        if (_backendHost == null)
            return;

        var selectedDate = PreviewDatePicker.Date.DateTime.Date;
        var requestVersion = Interlocked.Increment(ref _previewRequestVersion);
        SetPreviewLoadingState(selectedDate);

        var operationResult = await _backendHost.Backend.LoadDayAsync(selectedDate);
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
        PreviewStatusBar.Message = workflow.Message ?? "Preview loaded successfully.";

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

        if (string.IsNullOrWhiteSpace(previewLocation))
            return false;

        try
        {
            var bitmap = new BitmapImage();
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

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        if (_trayStatus != null)
            _trayStatus.Changed -= TrayStatus_Changed;

        base.OnNavigatedFrom(e);
    }
}
