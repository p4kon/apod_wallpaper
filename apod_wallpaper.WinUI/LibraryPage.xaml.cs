using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.System;

namespace apod_wallpaper.WinUI;

public sealed partial class LibraryPage : Page
{
    private LibraryPageArguments? _arguments;
    private apod_wallpaper.StorageSummary? _summary;

    public LibraryPage()
    {
        InitializeComponent();
        LocalizationHelper.ApplyTo(this);
        Loaded += LibraryPage_Loaded;
        AppStrings.LanguageChanged += AppStrings_LanguageChanged;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _arguments = e.Parameter as LibraryPageArguments;
        await LoadSummaryAsync();
    }

    private async void LibraryPage_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshLocalizedText();
        if (_arguments != null)
            await LoadSummaryAsync();
    }

    private void AppStrings_LanguageChanged(object? sender, EventArgs e)
    {
        RefreshLocalizedText();
        RebuildSummary();
    }

    private async void RefreshSummaryButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadSummaryAsync();
    }

    private async void OpenImagesFolderButton_Click(object sender, RoutedEventArgs e)
    {
        await OpenFolderAsync(_summary?.Paths?.ImagesDirectory, "Unable to open images folder");
    }

    private async void OpenDataFolderButton_Click(object sender, RoutedEventArgs e)
    {
        await OpenFolderAsync(_summary?.Paths?.ApplicationDataDirectory, "Unable to open data folder");
    }

    private async Task LoadSummaryAsync()
    {
        if (_arguments == null)
            return;

        LibraryStatusBar.Visibility = Visibility.Visible;
        LibraryStatusBar.Severity = InfoBarSeverity.Informational;
        LibraryStatusBar.Title = AppStrings.Get("Loading library summary");
        LibraryStatusBar.Message = string.Empty;
        SetActionButtonsEnabled(false);

        var result = await _arguments.BackendHost.Backend.GetStorageSummaryAsync();
        if (!result.Succeeded || result.Value == null)
        {
            _summary = null;
            RebuildSummary();
            LibraryStatusBar.Severity = InfoBarSeverity.Error;
            LibraryStatusBar.Title = AppStrings.Get("Unable to load library summary.");
            LibraryStatusBar.Message = AppStrings.GetBackendMessageOrDefault(result.Error?.Message, "Unable to summarize the local APOD library.");
            SetActionButtonsEnabled(false);
            return;
        }

        _summary = result.Value;
        RebuildSummary();
        LibraryStatusBar.Severity = InfoBarSeverity.Success;
        LibraryStatusBar.Title = AppStrings.Get("Library summary loaded");
        LibraryStatusBar.Message = AppStrings.Get("No files were changed.");
        SetActionButtonsEnabled(true);
    }

    private void RefreshLocalizedText()
    {
        LocalizationHelper.ApplyTo(this);
        AutomationProperties.SetName(OpenImagesFolderButton, AppStrings.Get("Open images folder"));
        AutomationProperties.SetName(OpenDataFolderButton, AppStrings.Get("Open data folder"));
        AutomationProperties.SetName(RefreshSummaryButton, AppStrings.Get("Refresh"));
    }

    private void RebuildSummary()
    {
        SummaryPanel.Children.Clear();
        if (_summary == null)
        {
            SummaryPanel.Children.Add(new TextBlock
            {
                Text = AppStrings.Get("Library summary unavailable"),
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
            return;
        }

        SummaryPanel.Children.Add(BuildMetricRow(
            "Downloaded images",
            AppStrings.Format("{0} files, {1}", _summary.DownloadedImageCount.ToString(CultureInfo.InvariantCulture), FormatBytes(_summary.DownloadedImageSizeBytes)),
            _summary.Paths.ImagesDirectory));
        SummaryPanel.Children.Add(BuildDirectoryRow("Images folder", _summary.Images));
        SummaryPanel.Children.Add(BuildDirectoryRow("Smart variants", _summary.SmartImages));
        SummaryPanel.Children.Add(BuildDirectoryRow("Cache", _summary.Cache));
        SummaryPanel.Children.Add(BuildDirectoryRow("Logs", _summary.Logs));
        SummaryPanel.Children.Add(BuildDirectoryRow("Application data", _summary.ApplicationData));
    }

    private FrameworkElement BuildDirectoryRow(string titleKey, apod_wallpaper.StorageDirectorySummary summary)
    {
        var value = summary == null
            ? AppStrings.Get("Not available")
            : AppStrings.Format(
                "{0} files, {1}",
                summary.FileCount.ToString(CultureInfo.InvariantCulture),
                FormatBytes(summary.SizeBytes));

        return BuildMetricRow(titleKey, value, summary?.Path);
    }

    private FrameworkElement BuildMetricRow(string titleKey, string value, string? path)
    {
        var root = new Border
        {
            Padding = new Thickness(14, 10, 14, 10),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
        };

        var grid = new Grid
        {
            ColumnSpacing = 12,
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var title = new TextBlock
        {
            Text = AppStrings.Get(titleKey),
            FontWeight = FontWeights.SemiBold,
        };
        Grid.SetColumn(title, 0);
        grid.Children.Add(title);

        var details = new StackPanel
        {
            Spacing = 2,
        };
        details.Children.Add(new TextBlock
        {
            Text = value,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
        details.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(path) ? AppStrings.Get("Not configured") : path,
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        Grid.SetColumn(details, 1);
        grid.Children.Add(details);

        root.Child = grid;
        return root;
    }

    private async Task OpenFolderAsync(string? path, string errorTitleKey)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            LibraryStatusBar.Severity = InfoBarSeverity.Warning;
            LibraryStatusBar.Title = AppStrings.Get(errorTitleKey);
            LibraryStatusBar.Message = AppStrings.Get("Folder path is not available.");
            return;
        }

        try
        {
            Directory.CreateDirectory(path);
            var folder = await StorageFolder.GetFolderFromPathAsync(path);
            var opened = await Launcher.LaunchFolderAsync(folder);
            if (!opened)
                throw new InvalidOperationException(AppStrings.Get("Windows did not open the folder."));
        }
        catch (Exception ex)
        {
            LibraryStatusBar.Severity = InfoBarSeverity.Error;
            LibraryStatusBar.Title = AppStrings.Get(errorTitleKey);
            LibraryStatusBar.Message = ex.Message;
        }
    }

    private void SetActionButtonsEnabled(bool isEnabled)
    {
        OpenImagesFolderButton.IsEnabled = isEnabled;
        OpenDataFolderButton.IsEnabled = isEnabled;
        RefreshSummaryButton.IsEnabled = _arguments != null;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return bytes.ToString(CultureInfo.InvariantCulture) + " B";

        var value = bytes / 1024d;
        var units = new[] { "KB", "MB", "GB", "TB" };
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024d;
            unitIndex++;
        }

        return value.ToString(value >= 10 ? "0.#" : "0.##", CultureInfo.InvariantCulture) + " " + units[unitIndex];
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        AppStrings.LanguageChanged -= AppStrings_LanguageChanged;
        base.OnNavigatedFrom(e);
    }
}
