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
    private FavoritesPageArguments? _arguments;
    private IReadOnlyList<apod_wallpaper.FavoriteApodItem> _favoriteItems = Array.Empty<apod_wallpaper.FavoriteApodItem>();
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

    private async void FavoritesPage_Loaded(object sender, RoutedEventArgs e)
    {
        LocalizationHelper.ApplyTo(this);
        RebuildFavoritesList();
        if (_arguments != null)
            await LoadFavoritesAsync();
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
        FavoritesLoadingText.Text = AppStrings.Get("Loading favorite images");
        FavoritesLoadingPanel.Visibility = Visibility.Visible;
        FavoritesGridView.Visibility = Visibility.Collapsed;
        EmptyFavoritesPanel.Visibility = Visibility.Collapsed;

        var result = await _arguments.BackendHost.Backend.GetFavoriteApodsAsync();
        if (loadVersion != _loadVersion)
            return;

        FavoritesLoadingPanel.Visibility = Visibility.Collapsed;
        if (!result.Succeeded || result.Value == null)
        {
            _favoriteItems = Array.Empty<apod_wallpaper.FavoriteApodItem>();
            RebuildFavoritesList();
            return;
        }

        _favoriteItems = result.Value;
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
        ToolTipService.SetToolTip(root, AppStrings.Get("Open favorite in Calendar"));
        AutomationProperties.SetName(root, AppStrings.Get("Open favorite in Calendar"));

        var thumbnail = new Border
        {
            Width = 128,
            Height = 92,
            VerticalAlignment = VerticalAlignment.Top,
            CornerRadius = new CornerRadius(8),
            Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
            Child = new Image
            {
                Source = CreateImageSource(item.ImagePath),
                Stretch = Stretch.Uniform,
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

    private static ImageSource? CreateImageSource(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            return null;

        return new BitmapImage(new Uri(imagePath, UriKind.Absolute));
    }

    private void FavoritesGridView_ItemClick(object sender, ItemClickEventArgs e)
    {
        TryOpenFavoriteFromItem(e.ClickedItem);
    }

    private void FavoriteTile_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (TryOpenFavoriteFromItem(sender))
            e.Handled = true;
    }

    private bool TryOpenFavoriteFromItem(object? item)
    {
        if (item is FrameworkElement { Tag: DateTime date })
        {
            _arguments?.OpenFavoriteDate(date.Date);
            return true;
        }

        return false;
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

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        AppStrings.LanguageChanged -= AppStrings_LanguageChanged;
        base.OnNavigatedFrom(e);
    }
}
