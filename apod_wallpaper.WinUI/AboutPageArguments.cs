namespace apod_wallpaper.WinUI;

internal sealed class AboutPageArguments
{
    public AboutPageArguments(BackendHost backendHost)
    {
        BackendHost = backendHost;
    }

    public BackendHost BackendHost { get; }
}
