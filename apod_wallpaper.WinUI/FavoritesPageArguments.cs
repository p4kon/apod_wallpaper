using System;

namespace apod_wallpaper.WinUI;

internal sealed class FavoritesPageArguments
{
    public FavoritesPageArguments(BackendHost backendHost, Action<DateTime> openFavoriteDate)
    {
        BackendHost = backendHost;
        OpenFavoriteDate = openFavoriteDate;
    }

    public BackendHost BackendHost { get; }

    public Action<DateTime> OpenFavoriteDate { get; }
}
