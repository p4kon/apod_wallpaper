using System;
using System.Collections.Generic;
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
    private const int PreviewDecodePixelWidth = 1200;
    private static IReadOnlyList<apod_wallpaper.FavoriteApodItem> CachedFavoriteItems = Array.Empty<apod_wallpaper.FavoriteApodItem>();
    private static readonly Dictionary<string, BitmapImage> ThumbnailCache = new(StringComparer.OrdinalIgnoreCase);
    private static bool HasCachedFavoriteItems;
    private FavoritesPageArguments? _arguments;
    private IReadOnlyList<apod_wallpaper.FavoriteApodItem> _favoriteItems = Array.Empty<apod_wallpaper.FavoriteApodItem>();
    private DateTime? _previewedFavoriteDate;
    private int _loadVersion;

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
        RebuildFavoritesList();
    }

    private void AppStrings_LanguageChanged(object? sender, EventArgs e)
    {
        LocalizationHelper.ApplyTo(this);
        FavoritesLoadingText.Text = AppStrings.Get("Loading favorite images");
        UpdatePreviewTooltips();
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

        _favoriteItems = result.Value;
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
        ToolTipService.SetToolTip(root, AppStrings.Get("Preview favorite image"));
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

    private static ImageSource? CreatePreviewImageSource(string? imagePath)
    {
        return CreateImageSource(imagePath, PreviewDecodePixelWidth, useMemoryCache: false);
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

    private void FavoritesGridView_ItemClick(object sender, ItemClickEventArgs e)
    {
        TryShowFavoritePreviewFromItem(e.ClickedItem);
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
        _previewedFavoriteDate = item.Date.Date;
        FavoritePreviewImage.Source = CreatePreviewImageSource(item.ImagePath);
        FavoritePreviewSurface.MaxWidth = Math.Max(360, ActualWidth * 0.75);
        FavoritePreviewSurface.MaxHeight = Math.Max(320, ActualHeight * 0.75);
        UpdatePreviewTooltips();

        FavoritePreviewOverlay.Visibility = Visibility.Visible;
        FavoritePreviewOverlay.Opacity = 0;
        FavoritePreviewScaleTransform.ScaleX = 0.9;
        FavoritePreviewScaleTransform.ScaleY = 0.9;
        AnimatePreviewOverlay(1, 1, null);
    }

    private void UpdatePreviewTooltips()
    {
        var calendarText = AppStrings.Get("Open favorite in Calendar");
        var closeText = AppStrings.Get("Close preview");
        ToolTipService.SetToolTip(FavoritePreviewCalendarButton, calendarText);
        ToolTipService.SetToolTip(FavoritePreviewCloseButton, closeText);
        AutomationProperties.SetName(FavoritePreviewCalendarButton, calendarText);
        AutomationProperties.SetName(FavoritePreviewCloseButton, closeText);
    }

    private void FavoritePreviewOverlay_Tapped(object sender, TappedRoutedEventArgs e)
    {
        CloseFavoritePreview();
    }

    private void FavoritePreviewSurface_Tapped(object sender, TappedRoutedEventArgs e)
    {
        CloseFavoritePreview();
        e.Handled = true;
    }

    private void FavoritePreviewCloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseFavoritePreview();
    }

    private void FavoritePreviewCalendarButton_Click(object sender, RoutedEventArgs e)
    {
        var date = _previewedFavoriteDate;
        CloseFavoritePreview();
        if (date.HasValue)
            _arguments?.OpenFavoriteDate(date.Value.Date);
    }

    private void FavoritePreviewCommandButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
    }

    private void CloseFavoritePreview()
    {
        if (FavoritePreviewOverlay.Visibility != Visibility.Visible)
            return;

        AnimatePreviewOverlay(0, 0.94, () =>
        {
            FavoritePreviewOverlay.Visibility = Visibility.Collapsed;
            FavoritePreviewImage.Source = null;
            _previewedFavoriteDate = null;
        });
    }

    private async void RemoveFavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_arguments == null || sender is not Button { Tag: DateTime date })
            return;

        var removeButton = (Button)sender;
        removeButton.IsEnabled = false;

        var result = await _arguments.BackendHost.Backend.SetFavoriteAsync(date.Date, false);
        if (!result.Succeeded)
        {
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

    private void AnimatePreviewOverlay(double overlayOpacity, double scale, Action? completed)
    {
        var overlayAnimation = new DoubleAnimation
        {
            To = overlayOpacity,
            Duration = new Duration(TimeSpan.FromMilliseconds(170)),
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(overlayAnimation, FavoritePreviewOverlay);
        Storyboard.SetTargetProperty(overlayAnimation, "Opacity");

        var scaleXAnimation = new DoubleAnimation
        {
            To = scale,
            Duration = new Duration(TimeSpan.FromMilliseconds(170)),
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(scaleXAnimation, FavoritePreviewScaleTransform);
        Storyboard.SetTargetProperty(scaleXAnimation, "ScaleX");

        var scaleYAnimation = new DoubleAnimation
        {
            To = scale,
            Duration = new Duration(TimeSpan.FromMilliseconds(170)),
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(scaleYAnimation, FavoritePreviewScaleTransform);
        Storyboard.SetTargetProperty(scaleYAnimation, "ScaleY");

        var storyboard = new Storyboard();
        storyboard.Children.Add(overlayAnimation);
        storyboard.Children.Add(scaleXAnimation);
        storyboard.Children.Add(scaleYAnimation);
        if (completed != null)
            storyboard.Completed += (_, _) => completed();
        storyboard.Begin();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        AppStrings.LanguageChanged -= AppStrings_LanguageChanged;
        base.OnNavigatedFrom(e);
    }
}
