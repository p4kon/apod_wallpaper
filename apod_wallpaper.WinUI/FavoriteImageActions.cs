using System;
using System.Threading.Tasks;

namespace apod_wallpaper.WinUI;

internal sealed class FavoriteImageActions
{
    public FavoriteImageActions(
        Func<apod_wallpaper.FavoriteApodItem, Task> setAsWallpaperAsync,
        Action<apod_wallpaper.FavoriteApodItem> openInCalendar,
        Action<apod_wallpaper.FavoriteApodItem> openInFolder,
        Func<apod_wallpaper.FavoriteApodItem, Task<bool>> removeFromFavoritesAsync)
    {
        SetAsWallpaperAsync = setAsWallpaperAsync;
        OpenInCalendar = openInCalendar;
        OpenInFolder = openInFolder;
        RemoveFromFavoritesAsync = removeFromFavoritesAsync;
    }

    public Func<apod_wallpaper.FavoriteApodItem, Task> SetAsWallpaperAsync { get; }

    public Action<apod_wallpaper.FavoriteApodItem> OpenInCalendar { get; }

    public Action<apod_wallpaper.FavoriteApodItem> OpenInFolder { get; }

    public Func<apod_wallpaper.FavoriteApodItem, Task<bool>> RemoveFromFavoritesAsync { get; }
}
