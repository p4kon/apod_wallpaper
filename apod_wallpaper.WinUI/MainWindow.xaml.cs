using Microsoft.UI.Xaml;

namespace apod_wallpaper.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly BackendHost _backendHost;
    private readonly TraySpikeStatus _trayStatus;
    private readonly TrayIconController _trayIconController;

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
        _trayIconController.Initialize();
        Closed += MainWindow_Closed;

        RootFrame.Navigate(typeof(ShellPage), new ShellPageArguments(
            backendHost,
            initialization,
            _trayStatus,
            HideToTray,
            ExitApplicationAsync));
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

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _trayIconController.Dispose();
        _backendHost.Dispose();
    }
}
