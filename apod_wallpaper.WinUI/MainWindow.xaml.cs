using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using System;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;

namespace apod_wallpaper.WinUI;

public sealed partial class MainWindow : Window
{
    private const int PreferredWindowWidthDip = 860;
    private const int PreferredWindowHeightDip = 840;
    private const int MinimumWindowWidthPixels = 720;
    private const int MinimumWindowHeightPixels = 680;
    private const int WorkAreaMarginPixels = 32;

    private readonly BackendHost _backendHost;
    private readonly TraySpikeStatus _trayStatus;
    private readonly TrayIconController _trayIconController;
    private bool _minimizeToTrayOnClose = true;

    internal MainWindow(
        BackendHost backendHost,
        apod_wallpaper.OperationResult<apod_wallpaper.ApplicationSettingsSnapshot> initialization)
    {
        InitializeComponent();
        _backendHost = backendHost;
        _trayStatus = new TraySpikeStatus();
        _trayIconController = new TrayIconController(this, _trayStatus);

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        var fixedWindowSize = ResolveFixedWindowSize();
        AppWindow.Resize(fixedWindowSize);
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }

        _trayIconController.SetMinimumWindowSize(fixedWindowSize.Width, fixedWindowSize.Height);
        if (initialization.Succeeded && initialization.Value != null)
            SetCloseBehavior(initialization.Value.MinimizeToTrayOnClose);
        _trayIconController.Initialize();
        Closed += MainWindow_Closed;

        RootFrame.Navigate(typeof(ShellPage), new ShellPageArguments(
            backendHost,
            initialization,
            _trayStatus,
            HideToTray,
            ExitApplicationAsync,
            SetCloseBehavior));
    }

    internal void HideToTray()
    {
        _trayIconController.HideToTray();
    }

    internal async System.Threading.Tasks.Task ExitApplicationAsync()
    {
        var shutdownResult = await _backendHost.ShutdownAsync();
        if (!shutdownResult.Succeeded)
            throw new InvalidOperationException(shutdownResult.Error != null ? shutdownResult.Error.Message : "Unable to shut down backend host.");

        _trayIconController.AllowClose();
        Close();
    }

    internal void SetCloseBehavior(bool minimizeToTrayOnClose)
    {
        _minimizeToTrayOnClose = minimizeToTrayOnClose;
        _trayIconController.SetCloseBehavior(minimizeToTrayOnClose);
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _trayIconController.Dispose();
        _backendHost.Dispose();
    }

    private SizeInt32 ResolveFixedWindowSize()
    {
        var scale = ResolveRasterizationScale();
        var preferredWidth = (int)Math.Ceiling(PreferredWindowWidthDip * scale);
        var preferredHeight = (int)Math.Ceiling(PreferredWindowHeightDip * scale);
        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;

        var maxWidth = Math.Max(1, workArea.Width - WorkAreaMarginPixels);
        var maxHeight = Math.Max(1, workArea.Height - WorkAreaMarginPixels);

        return new SizeInt32(
            Math.Min(Math.Max(MinimumWindowWidthPixels, preferredWidth), maxWidth),
            Math.Min(Math.Max(MinimumWindowHeightPixels, preferredHeight), maxHeight));
    }

    private double ResolveRasterizationScale()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var dpi = GetDpiForWindow(hwnd);
        return dpi > 0 ? dpi / 96d : 1d;
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
