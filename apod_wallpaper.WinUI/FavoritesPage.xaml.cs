using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;

namespace apod_wallpaper.WinUI;

public sealed partial class FavoritesPage : Page
{
    private const int ThumbnailDecodePixelWidth = 180;
    private const int ThumbnailCacheLimit = 160;
    private static IReadOnlyList<apod_wallpaper.FavoriteApodItem> CachedFavoriteItems = Array.Empty<apod_wallpaper.FavoriteApodItem>();
    private static readonly Dictionary<string, BitmapImage> ThumbnailCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<FavoriteImagePreviewWindow> OpenPreviewWindows = new();
    private static bool HasCachedFavoriteItems;
    private FavoritesPageArguments? _arguments;
    private IReadOnlyList<apod_wallpaper.FavoriteApodItem> _favoriteItems = Array.Empty<apod_wallpaper.FavoriteApodItem>();
    private int _loadVersion;
    private bool _isApplyingFavoriteWallpaper;
    private bool _previewOpenInProgress;

    public FavoritesPage()
    {
        InitializeComponent();
        LocalizationHelper.ApplyTo(this);
        Loaded += FavoritesPage_Loaded;
        AppStrings.LanguageChanged += AppStrings_LanguageChanged;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _arguments = e.Parameter as FavoritesPageArguments;
        await LoadFavoritesAsync();
    }

    private void FavoritesPage_Loaded(object sender, RoutedEventArgs e)
    {
        LocalizationHelper.ApplyTo(this);
        if (_favoriteItems.Count > 0)
            RebuildFavoritesList();
    }

    private void AppStrings_LanguageChanged(object? sender, EventArgs e)
    {
        LocalizationHelper.ApplyTo(this);
        FavoritesLoadingText.Text = AppStrings.Get("Loading favorite images");
        RebuildFavoritesList();
    }

    private async Task LoadFavoritesAsync()
    {
        if (_arguments == null)
            return;

        var loadVersion = Interlocked.Increment(ref _loadVersion);
        var hasCachedItems = HasCachedFavoriteItems;
        if (hasCachedItems)
        {
            _favoriteItems = CachedFavoriteItems;
            FavoritesLoadingPanel.Visibility = Visibility.Collapsed;
            RebuildFavoritesList();
        }
        else
        {
            FavoritesLoadingText.Text = AppStrings.Get("Loading favorite images");
            FavoritesLoadingPanel.Visibility = Visibility.Visible;
            FavoritesGridView.Visibility = Visibility.Collapsed;
            EmptyFavoritesPanel.Visibility = Visibility.Collapsed;
        }

        var result = await _arguments.BackendHost.Backend.GetFavoriteApodsAsync();
        if (loadVersion != _loadVersion)
            return;

        FavoritesLoadingPanel.Visibility = Visibility.Collapsed;
        if (!result.Succeeded || result.Value == null)
        {
            if (!hasCachedItems)
            {
                _favoriteItems = Array.Empty<apod_wallpaper.FavoriteApodItem>();
                RebuildFavoritesList();
            }

            return;
        }

        var refreshedItems = result.Value;
        if (hasCachedItems && AreFavoriteItemsEquivalent(CachedFavoriteItems, refreshedItems))
            return;

        _favoriteItems = refreshedItems;
        CachedFavoriteItems = _favoriteItems;
        HasCachedFavoriteItems = true;
        RebuildFavoritesList();
    }

    private void RebuildFavoritesList()
    {
        FavoritesGridView.Items.Clear();
        var hasItems = _favoriteItems.Count > 0;
        FavoritesGridView.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        EmptyFavoritesPanel.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;

        foreach (var item in _favoriteItems)
        {
            FavoritesGridView.Items.Add(new GridViewItem
            {
                Content = BuildFavoriteTile(item),
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 10, 12),
                Width = 128,
                Height = 132,
                Tag = item.Date.Date,
            });
        }
    }

    private FrameworkElement BuildFavoriteTile(apod_wallpaper.FavoriteApodItem item)
    {
        var root = new Grid
        {
            Width = 128,
            Height = 132,
            Tag = item.Date.Date,
        };
        AutomationProperties.SetName(root, AppStrings.Get("Preview favorite image"));

        var thumbnail = new Border
        {
            Width = 128,
            Height = 92,
            VerticalAlignment = VerticalAlignment.Top,
            CornerRadius = new CornerRadius(8),
            Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
            Child = new Image
            {
                Source = CreateThumbnailImageSource(item),
                Stretch = Stretch.UniformToFill,
            },
        };
        root.Children.Add(thumbnail);

        var dateText = new TextBlock
        {
            Margin = new Thickness(2, 98, 2, 0),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Text = item.Date.ToString("dd MMM yyyy", AppStrings.DateCulture),
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };
        root.Children.Add(dateText);

        var titleText = new TextBlock
        {
            Margin = new Thickness(2, 116, 2, 0),
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            Text = BuildTileTitle(item),
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };
        root.Children.Add(titleText);

        var removeButton = new Button
        {
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 6, 6, 0),
            Opacity = 0,
            Tag = item.Date.Date,
            Content = new FontIcon
            {
                Glyph = "\uE711",
                FontSize = 11,
            },
        };
        removeButton.Click += RemoveFavoriteButton_Click;
        removeButton.Tapped += (_, args) => args.Handled = true;
        removeButton.PointerPressed += (_, args) => args.Handled = true;
        AutomationProperties.SetName(removeButton, AppStrings.Get("Remove from favorites"));
        ToolTipService.SetToolTip(removeButton, AppStrings.Get("Remove from favorites"));
        root.Children.Add(removeButton);

        root.PointerEntered += (_, _) => AnimateOpacity(removeButton, 1);
        root.PointerExited += (_, _) => AnimateOpacity(removeButton, 0);
        root.Tapped += FavoriteTile_Tapped;
        root.RightTapped += (_, args) =>
        {
            args.Handled = true;
            ShowFavoriteContextMenu(root, item);
        };

        return root;
    }

    private static string BuildTileTitle(apod_wallpaper.FavoriteApodItem item)
    {
        var fallbackTitle = "APOD " + item.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(item.Title) ||
            string.Equals(item.Title.Trim(), fallbackTitle, StringComparison.OrdinalIgnoreCase))
        {
            return AppStrings.Get("APOD image");
        }

        return item.Title.Trim();
    }

    private static ImageSource? CreateThumbnailImageSource(apod_wallpaper.FavoriteApodItem item)
    {
        var imagePath = !string.IsNullOrWhiteSpace(item.ThumbnailPath)
            ? item.ThumbnailPath
            : item.ImagePath;
        return CreateImageSource(imagePath, ThumbnailDecodePixelWidth, useMemoryCache: true);
    }

    private static ImageSource? CreateImageSource(string? imagePath, int decodePixelWidth, bool useMemoryCache)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            return null;

        if (useMemoryCache && ThumbnailCache.TryGetValue(imagePath, out var cachedImage))
            return cachedImage;

        if (useMemoryCache && ThumbnailCache.Count >= ThumbnailCacheLimit)
            ThumbnailCache.Clear();

        var image = new BitmapImage
        {
            DecodePixelWidth = decodePixelWidth,
        };
        image.UriSource = new Uri(imagePath, UriKind.Absolute);
        if (useMemoryCache)
            ThumbnailCache[imagePath] = image;
        return image;
    }

    private void FavoriteTile_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (TryShowFavoritePreviewFromItem(sender))
            e.Handled = true;
    }

    private bool TryShowFavoritePreviewFromItem(object? item)
    {
        if (item is FrameworkElement { Tag: DateTime date })
        {
            var favorite = _favoriteItems.FirstOrDefault(candidate => candidate.Date.Date == date.Date);
            if (favorite != null)
                ShowFavoritePreview(favorite);
            return true;
        }

        return false;
    }

    private void ShowFavoritePreview(apod_wallpaper.FavoriteApodItem item)
    {
        if (string.IsNullOrWhiteSpace(item.ImagePath) || !File.Exists(item.ImagePath))
            return;

        if (_previewOpenInProgress || OpenPreviewWindows.Count > 0)
            return;

        FavoriteImagePreviewWindow? window = null;
        try
        {
            _previewOpenInProgress = true;
            window = new FavoriteImagePreviewWindow(item.ImagePath, item.Date.Date, date => _arguments?.OpenFavoriteDate(date));
            OpenPreviewWindows.Add(window);
            window.Closed += (_, _) =>
            {
                OpenPreviewWindows.Remove(window);
                _previewOpenInProgress = false;
            };
            window.ShowPreview();
        }
        catch
        {
            if (window != null)
                OpenPreviewWindows.Remove(window);
            _previewOpenInProgress = false;
            throw;
        }
    }

    private void ShowFavoriteContextMenu(FrameworkElement target, apod_wallpaper.FavoriteApodItem item)
    {
        var menu = new MenuFlyout();
        var setWallpaperItem = new MenuFlyoutItem
        {
            Text = AppStrings.Get("Set as wallpaper"),
            Icon = new FontIcon { Glyph = "\uE771" },
        };
        setWallpaperItem.Click += async (_, _) => await SetFavoriteAsWallpaperAsync(item);

        var openCalendarItem = new MenuFlyoutItem
        {
            Text = AppStrings.Get("Open favorite in Calendar"),
            Icon = new FontIcon { Glyph = "\uE787" },
        };
        openCalendarItem.Click += (_, _) => _arguments?.OpenFavoriteDate(item.Date.Date);

        var openFolderItem = new MenuFlyoutItem
        {
            Text = AppStrings.Get("Open in folder"),
            Icon = new FontIcon { Glyph = "\uE838" },
        };
        openFolderItem.Click += (_, _) => OpenImageInFolder(item.ImagePath);

        var removeFavoriteItem = new MenuFlyoutItem
        {
            Text = AppStrings.Get("Remove from favorites"),
            Icon = new FontIcon { Glyph = "\uE711" },
        };
        removeFavoriteItem.Click += async (_, _) => await RemoveFavoriteAsync(item.Date.Date, null);

        menu.Items.Add(setWallpaperItem);
        menu.Items.Add(openCalendarItem);
        menu.Items.Add(openFolderItem);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(removeFavoriteItem);
        menu.ShowAt(target);
    }

    private static void OpenImageInFolder(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = "/select,\"" + imagePath + "\"",
            UseShellExecute = true,
        });
    }

    private async void RemoveFavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: DateTime date } removeButton)
            return;

        await RemoveFavoriteAsync(date.Date, removeButton);
    }

    private async Task RemoveFavoriteAsync(DateTime date, Button? removeButton)
    {
        if (_arguments == null)
            return;

        if (removeButton != null)
            removeButton.IsEnabled = false;

        var result = await _arguments.BackendHost.Backend.SetFavoriteAsync(date.Date, false);
        if (!result.Succeeded)
        {
            if (removeButton != null)
                removeButton.IsEnabled = true;
            return;
        }

        await FadeOutFavoriteItemAsync(date.Date);
        _favoriteItems = _favoriteItems.Where(item => item.Date.Date != date.Date).ToList();
        CachedFavoriteItems = _favoriteItems;
        HasCachedFavoriteItems = true;
        RemoveFavoriteGridItem(date.Date);
        EmptyFavoritesPanel.Visibility = _favoriteItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FavoritesGridView.Visibility = _favoriteItems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task SetFavoriteAsWallpaperAsync(apod_wallpaper.FavoriteApodItem item)
    {
        if (_arguments == null || _isApplyingFavoriteWallpaper)
            return;

        if (string.IsNullOrWhiteSpace(item.ImagePath) || !File.Exists(item.ImagePath))
            return;

        _isApplyingFavoriteWallpaper = true;
        try
        {
            var settingsResult = await _arguments.BackendHost.Backend.GetSettingsAsync();
            var wallpaperStyle = ResolveWallpaperStyleFromSettings(settingsResult.Value);
            var applyResult = await _arguments.BackendHost.Backend.ApplyDayAsync(item.Date.Date, wallpaperStyle);
            if (!applyResult.Succeeded || applyResult.Value == null)
                return;

            await DisableAutoRefreshAfterFavoriteWallpaperApplyAsync();
        }
        finally
        {
            _isApplyingFavoriteWallpaper = false;
        }
    }

    private async Task DisableAutoRefreshAfterFavoriteWallpaperApplyAsync()
    {
        if (_arguments == null)
            return;

        var settingsResult = await _arguments.BackendHost.Backend.GetSettingsAsync();
        if (!settingsResult.Succeeded || settingsResult.Value == null || !settingsResult.Value.AutoRefreshEnabled)
            return;

        var updatedSnapshot = settingsResult.Value.Clone();
        updatedSnapshot.AutoRefreshEnabled = false;
        await _arguments.BackendHost.Backend.SaveSettingsAsync(updatedSnapshot);
    }

    private static apod_wallpaper.WallpaperStyle ResolveWallpaperStyleFromSettings(apod_wallpaper.ApplicationSettingsSnapshot? settings)
    {
        if (settings != null && Enum.IsDefined(typeof(apod_wallpaper.WallpaperStyle), settings.WallpaperStyleIndex))
            return (apod_wallpaper.WallpaperStyle)settings.WallpaperStyleIndex;

        return apod_wallpaper.WallpaperStyle.Smart;
    }

    private Task FadeOutFavoriteItemAsync(DateTime date)
    {
        foreach (var item in FavoritesGridView.Items)
        {
            if (item is GridViewItem { Tag: DateTime itemDate } gridItem && itemDate == date)
                return AnimateOpacityAsync(gridItem, 0);
        }

        return Task.CompletedTask;
    }

    private void RemoveFavoriteGridItem(DateTime date)
    {
        for (var i = FavoritesGridView.Items.Count - 1; i >= 0; i--)
        {
            if (FavoritesGridView.Items[i] is GridViewItem { Tag: DateTime itemDate } && itemDate == date)
                FavoritesGridView.Items.RemoveAt(i);
        }
    }

    private static void AnimateOpacity(UIElement target, double to)
    {
        var animation = new DoubleAnimation
        {
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(140)),
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, "Opacity");
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    private static Task AnimateOpacityAsync(UIElement target, double to)
    {
        var completion = new TaskCompletionSource<bool>();
        var animation = new DoubleAnimation
        {
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, "Opacity");
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Completed += (_, _) => completion.TrySetResult(true);
        storyboard.Begin();
        return completion.Task;
    }

    private static bool AreFavoriteItemsEquivalent(
        IReadOnlyList<apod_wallpaper.FavoriteApodItem> first,
        IReadOnlyList<apod_wallpaper.FavoriteApodItem> second)
    {
        if (first.Count != second.Count)
            return false;

        for (var i = 0; i < first.Count; i++)
        {
            var a = first[i];
            var b = second[i];
            if (a.Date.Date != b.Date.Date ||
                !string.Equals(a.ImagePath, b.ImagePath, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(a.ThumbnailPath, b.ThumbnailPath, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(a.Title, b.Title, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        AppStrings.LanguageChanged -= AppStrings_LanguageChanged;
        base.OnNavigatedFrom(e);
    }
}
