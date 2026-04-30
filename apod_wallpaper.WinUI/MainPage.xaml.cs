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

        SnapshotSummaryText.Text = string.Join(Environment.NewLine, new[]
        {
            "Preferred date: " + snapshot.PreferredDisplayDate.ToString("yyyy-MM-dd"),
            "Wallpaper style: " + snapshot.SelectedWallpaperStyle,
            "API key state: " + snapshot.ApiKeyValidationState,
            "Storage mode: " + snapshot.StoragePaths.Mode,
            "Images directory: " + snapshot.StoragePaths.ImagesDirectory,
            "Settings file: " + snapshot.StoragePaths.SettingsFilePath,
            "Local image index ready: " + snapshot.LocalImageIndexReady,
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
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        if (_trayStatus != null)
            _trayStatus.Changed -= TrayStatus_Changed;

        base.OnNavigatedFrom(e);
    }
}
