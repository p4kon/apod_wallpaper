using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace apod_wallpaper.WinUI;

public sealed partial class MainPage : Page
{
    private BackendHost? _backendHost;
    private apod_wallpaper.OperationResult<apod_wallpaper.ApplicationSettingsSnapshot>? _initialization;
    private TraySpikeStatus? _trayStatus;
    private Action? _hideWindowToTray;
    private Func<System.Threading.Tasks.Task>? _exitApplicationAsync;

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

        await RefreshSnapshotAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshSnapshotAsync();
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

    private async System.Threading.Tasks.Task RefreshSnapshotAsync()
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

        var snapshot = snapshotResult.Value;
        _trayStatus?.MarkBackendCheck();
        StatusBar.Severity = InfoBarSeverity.Success;
        StatusBar.Title = "Backend initialized";
        StatusBar.Message = "The WinUI host created ApplicationController and loaded the initial snapshot in one backend call.";

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

        SnapshotSummaryText.Text = string.Join(Environment.NewLine, new[]
        {
            "One backend call returned both current state and persisted settings.",
            "This screen is intentionally readonly for now.",
            "Next step will turn these same state blocks into live settings controls without adding duplicate screens.",
            "Settings file: " + snapshot.StoragePaths.SettingsFilePath,
        });
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
