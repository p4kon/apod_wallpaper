namespace apod_wallpaper.WinUI;

internal sealed class LibraryPageArguments
{
    public LibraryPageArguments(BackendHost backendHost)
    {
        BackendHost = backendHost;
    }

    public BackendHost BackendHost { get; }
}
