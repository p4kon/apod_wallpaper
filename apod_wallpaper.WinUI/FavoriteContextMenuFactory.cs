using Microsoft.UI.Xaml.Controls;

namespace apod_wallpaper.WinUI;

internal static class FavoriteContextMenuFactory
{
    public static MenuFlyout Create(apod_wallpaper.FavoriteApodItem item, FavoriteImageActions actions)
    {
        var menu = new MenuFlyout();

        var setWallpaperItem = new MenuFlyoutItem
        {
            Text = AppStrings.Get("Set as wallpaper"),
            Icon = new FontIcon { Glyph = "\uE771" },
        };
        setWallpaperItem.Click += async (_, _) => await actions.SetAsWallpaperAsync(item);

        var openCalendarItem = new MenuFlyoutItem
        {
            Text = AppStrings.Get("Open favorite in Calendar"),
            Icon = new FontIcon { Glyph = "\uE787" },
        };
        openCalendarItem.Click += (_, _) => actions.OpenInCalendar(item);

        var openFolderItem = new MenuFlyoutItem
        {
            Text = AppStrings.Get("Open in folder"),
            Icon = new FontIcon { Glyph = "\uE838" },
        };
        openFolderItem.Click += (_, _) => actions.OpenInFolder(item);

        var removeFavoriteItem = new MenuFlyoutItem
        {
            Text = AppStrings.Get("Remove from favorites"),
            Icon = new FontIcon { Glyph = "\uE711" },
        };
        removeFavoriteItem.Click += async (_, _) => await actions.RemoveFromFavoritesAsync(item);

        menu.Items.Add(setWallpaperItem);
        menu.Items.Add(openCalendarItem);
        menu.Items.Add(openFolderItem);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(removeFavoriteItem);
        return menu;
    }
}
