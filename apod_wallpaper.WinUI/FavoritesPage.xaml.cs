using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;

namespace apod_wallpaper.WinUI;

public sealed partial class FavoritesPage : Page
{
    private FavoritesPageArguments? _arguments;
    private IReadOnlyList<apod_wallpaper.FavoriteApodItem> _favoriteItems = Array.Empty<apod_wallpaper.FavoriteApodItem>();

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
        RebuildFavoritesList();
    }

    private async Task LoadFavoritesAsync()
    {
        if (_arguments == null)
            return;

        FavoritesStatusBar.Visibility = Visibility.Visible;
        FavoritesStatusBar.Severity = InfoBarSeverity.Informational;
        FavoritesStatusBar.Title = AppStrings.Get("Loading favorite images");
        FavoritesStatusBar.Message = string.Empty;

        var result = await _arguments.BackendHost.Backend.GetFavoriteApodsAsync();
        if (!result.Succeeded || result.Value == null)
        {
            _favoriteItems = Array.Empty<apod_wallpaper.FavoriteApodItem>();
            RebuildFavoritesList();
            FavoritesStatusBar.Severity = InfoBarSeverity.Error;
            FavoritesStatusBar.Title = AppStrings.Get("Unable to load favorite images.");
            FavoritesStatusBar.Message = AppStrings.GetBackendMessageOrDefault(result.Error?.Message, "Unable to load favorite images.");
            return;
        }

        _favoriteItems = result.Value;
        RebuildFavoritesList();
        FavoritesStatusBar.Severity = InfoBarSeverity.Success;
        FavoritesStatusBar.Title = AppStrings.Get("Favorite images loaded");
        FavoritesStatusBar.Message = AppStrings.Format("{0} favorite images.", _favoriteItems.Count.ToString(CultureInfo.InvariantCulture));
    }

    private void RebuildFavoritesList()
    {
        FavoritesListView.Items.Clear();
        var hasItems = _favoriteItems.Count > 0;
        FavoritesListView.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        EmptyFavoritesPanel.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;

        foreach (var item in _favoriteItems)
        {
            FavoritesListView.Items.Add(new ListViewItem
            {
                Content = BuildFavoriteItem(item),
                Padding = new Thickness(0),
            });
        }
    }

    private FrameworkElement BuildFavoriteItem(apod_wallpaper.FavoriteApodItem item)
    {
        var root = new Grid
        {
            Padding = new Thickness(0, 8, 0, 8),
            ColumnSpacing = 12,
        };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var thumbnail = new Border
        {
            Width = 112,
            Height = 68,
            CornerRadius = new CornerRadius(10),
            Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
            Child = new Image
            {
                Source = CreateImageSource(item.ImagePath),
                Stretch = Stretch.UniformToFill,
            },
        };
        Grid.SetColumn(thumbnail, 0);
        root.Children.Add(thumbnail);

        var textPanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 2,
        };
        textPanel.Children.Add(new TextBlock
        {
            Text = item.Date.ToString("dddd, dd MMMM yyyy", AppStrings.DateCulture),
            FontWeight = FontWeights.SemiBold,
        });
        textPanel.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(item.Title) ? AppStrings.Get("APOD image") : item.Title,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        Grid.SetColumn(textPanel, 1);
        root.Children.Add(textPanel);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 6,
        };

        var openButton = new Button
        {
            Content = AppStrings.Get("Open"),
            Tag = item.Date,
            MinWidth = 74,
        };
        openButton.Click += OpenFavoriteButton_Click;
        AutomationProperties.SetName(openButton, AppStrings.Get("Open"));
        ToolTipService.SetToolTip(openButton, AppStrings.Get("Open favorite in Calendar"));
        actions.Children.Add(openButton);

        var removeButton = new Button
        {
            Content = AppStrings.Get("Remove"),
            Tag = item.Date,
            MinWidth = 74,
        };
        removeButton.Click += RemoveFavoriteButton_Click;
        AutomationProperties.SetName(removeButton, AppStrings.Get("Remove from favorites"));
        ToolTipService.SetToolTip(removeButton, AppStrings.Get("Remove from favorites"));
        actions.Children.Add(removeButton);

        Grid.SetColumn(actions, 2);
        root.Children.Add(actions);

        return root;
    }

    private static ImageSource? CreateImageSource(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            return null;

        return new BitmapImage(new Uri(imagePath, UriKind.Absolute));
    }

    private void OpenFavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DateTime date })
            _arguments?.OpenFavoriteDate(date.Date);
    }

    private async void RemoveFavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_arguments == null || sender is not Button { Tag: DateTime date })
            return;

        FavoritesStatusBar.Visibility = Visibility.Visible;
        FavoritesStatusBar.Severity = InfoBarSeverity.Informational;
        FavoritesStatusBar.Title = AppStrings.Get("Removing favorite");
        FavoritesStatusBar.Message = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var result = await _arguments.BackendHost.Backend.SetFavoriteAsync(date.Date, false);
        if (!result.Succeeded)
        {
            FavoritesStatusBar.Severity = InfoBarSeverity.Error;
            FavoritesStatusBar.Title = AppStrings.Get("Favorite was not removed");
            FavoritesStatusBar.Message = AppStrings.GetBackendMessageOrDefault(result.Error?.Message, "Unable to remove favorite.");
            return;
        }

        await LoadFavoritesAsync();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        AppStrings.LanguageChanged -= AppStrings_LanguageChanged;
        base.OnNavigatedFrom(e);
    }
}
