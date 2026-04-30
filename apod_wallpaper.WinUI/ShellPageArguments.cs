using System;
using System.Threading.Tasks;

namespace apod_wallpaper.WinUI;

internal sealed class ShellPageArguments
{
    public ShellPageArguments(
        BackendHost backendHost,
        apod_wallpaper.OperationResult<apod_wallpaper.ApplicationSettingsSnapshot> initialization,
        TraySpikeStatus trayStatus,
        Action hideWindowToTray,
        Func<Task> exitApplicationAsync)
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

    public Func<Task> ExitApplicationAsync { get; }

    public MainPageArguments CreateMainPageArguments()
    {
        return new MainPageArguments(
            BackendHost,
            Initialization,
            TrayStatus,
            HideWindowToTray,
            ExitApplicationAsync);
    }
}
