namespace apod_wallpaper.WinUI;

internal sealed class MainPageArguments
{
    public MainPageArguments(
        BackendHost backendHost,
        apod_wallpaper.OperationResult<apod_wallpaper.ApplicationSettingsSnapshot> initialization,
        TraySpikeStatus trayStatus,
        Action hideWindowToTray,
        Func<System.Threading.Tasks.Task> exitApplicationAsync)
    {
        BackendHost = backendHost;
        Initialization = initialization;
        TrayStatus = trayStatus;
        HideWindowToTray = hideWindowToTray;
        ExitApplicationAsync = exitApplicationAsync;
    }

    public BackendHost BackendHost { get; }

    public apod_wallpaper.OperationResult<apod_wallpaper.ApplicationSettingsSnapshot> Initialization { get; }

    public TraySpikeStatus TrayStatus { get; }

    public Action HideWindowToTray { get; }

    public Func<System.Threading.Tasks.Task> ExitApplicationAsync { get; }
}
