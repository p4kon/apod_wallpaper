namespace apod_wallpaper.WinUI;

internal sealed class MainPageArguments
{
    public MainPageArguments(
        BackendHost backendHost,
        apod_wallpaper.OperationResult<apod_wallpaper.ApplicationSettingsSnapshot> initialization)
    {
        BackendHost = backendHost;
        Initialization = initialization;
    }

    public BackendHost BackendHost { get; }

    public apod_wallpaper.OperationResult<apod_wallpaper.ApplicationSettingsSnapshot> Initialization { get; }
}
