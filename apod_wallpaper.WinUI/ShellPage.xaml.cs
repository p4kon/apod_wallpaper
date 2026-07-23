using System;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace apod_wallpaper.WinUI;

public sealed partial class ShellPage : Page
{
    private ShellPageArguments? _arguments;
    private bool _automaticUpdateCheckStarted;
    private static readonly SolidColorBrush ActiveNavBrush = new(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x00, 0x78, 0xD4));
    private static readonly SolidColorBrush InactiveNavBrush = new(Microsoft.UI.Colors.Transparent);

    public ShellPage()
    {
        InitializeComponent();
        LocalizationHelper.ApplyTo(this);
        ApplyNavigationLabels();
        AppStrings.LanguageChanged += AppStrings_LanguageChanged;
        Loaded += ShellPage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        _arguments = e.Parameter as ShellPageArguments;
        if (_arguments == null)
            return;

        NavigateToPreview();
    }

    private void PreviewButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToPreview();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSettings();
    }

    private void FavoritesButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToFavorites();
    }

    private void LibraryButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToLibrary();
    }

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToAbout();
    }

    private void HideToTrayButton_Click(object sender, RoutedEventArgs e)
    {
        _arguments?.HideWindowToTray();
    }

    private void AppStrings_LanguageChanged(object? sender, System.EventArgs e)
    {
        LocalizationHelper.ApplyTo(this);
        ApplyNavigationLabels();
    }

    private async void ShellPage_Loaded(object sender, RoutedEventArgs e)
    {
        LocalizationHelper.ApplyTo(this);
        ApplyNavigationLabels();
        await TryRunAutomaticUpdateCheckAsync();
    }

    private void NavigateToPreview()
    {
        if (_arguments == null)
            return;

        SetActiveButton(PreviewButton);
        ContentFrame.Navigate(typeof(MainPage), _arguments.CreateMainPageArguments());
        NotifyCalendarHostReturned();
    }

    internal void NotifyWindowActivated()
    {
        NotifyCalendarHostReturned();
    }

    internal void NotifyRestoredFromTray()
    {
        NotifyCalendarHostReturned();
    }

    private void NavigateToSettings()
    {
        if (_arguments == null)
            return;

        SetActiveButton(SettingsButton);
        ContentFrame.Navigate(typeof(SettingsPage), _arguments.CreateSettingsPageArguments());
    }

    private void NavigateToFavorites()
    {
        if (_arguments == null)
            return;

        SetActiveButton(FavoritesButton);
        ContentFrame.Navigate(typeof(FavoritesPage), _arguments.CreateFavoritesPageArguments(OpenFavoriteDate));
    }

    private void NavigateToLibrary()
    {
        if (_arguments == null)
            return;

        SetActiveButton(LibraryButton);
        ContentFrame.Navigate(typeof(LibraryPage), _arguments.CreateLibraryPageArguments());
    }

    private void OpenFavoriteDate(DateTime date)
    {
        if (_arguments == null)
            return;

        SetActiveButton(PreviewButton);
        ContentFrame.Navigate(typeof(MainPage), _arguments.CreateMainPageArguments(date.Date));
        NotifyCalendarHostReturned();
    }

    private void NavigateToAbout()
    {
        SetActiveButton(AboutButton);
        ContentFrame.Navigate(typeof(AboutPage), _arguments?.CreateAboutPageArguments());
    }

    private async System.Threading.Tasks.Task TryRunAutomaticUpdateCheckAsync()
    {
        if (_automaticUpdateCheckStarted || _arguments == null)
            return;

        _automaticUpdateCheckStarted = true;
        var settingsResult = await _arguments.BackendHost.Backend.GetSettingsAsync();
        if (!settingsResult.Succeeded || settingsResult.Value == null)
            return;

        var settings = settingsResult.Value;
        if (!settings.AutoCheckUpdatesEnabled)
            return;

        var currentVersion = AppVersionResolver.ResolveCurrentVersionText();
        var checkResult = await _arguments.BackendHost.Backend.CheckForUpdatesAsync(currentVersion, forceCheck: false, automatic: true);
        if (!checkResult.Succeeded || checkResult.Value == null || checkResult.Value.Status != apod_wallpaper.UpdateCheckStatus.UpdateAvailable)
            return;

        if (settings.SuppressAutomaticUpdateReminder)
            return;

        var choice = await UpdateNotificationDialog.ShowAsync(XamlRoot, checkResult.Value, includeDoNotRemind: true);
        if (choice == UpdateDialogChoice.OpenRelease)
        {
            await OpenReleaseAsync(checkResult.Value);
            return;
        }

        if (choice == UpdateDialogChoice.DoNotRemind)
            await SuppressAutomaticUpdateReminderAsync();
    }

    private async System.Threading.Tasks.Task SuppressAutomaticUpdateReminderAsync()
    {
        if (_arguments == null)
            return;

        var settingsResult = await _arguments.BackendHost.Backend.GetSettingsAsync();
        if (!settingsResult.Succeeded || settingsResult.Value == null)
            return;

        var settings = settingsResult.Value.Clone();
        settings.SuppressAutomaticUpdateReminder = true;
        await _arguments.BackendHost.Backend.SaveSettingsAsync(settings);
    }

    private static async System.Threading.Tasks.Task OpenReleaseAsync(apod_wallpaper.UpdateCheckResult result)
    {
        if (string.IsNullOrWhiteSpace(result.LatestReleaseUrl))
            return;

        await Launcher.LaunchUriAsync(new System.Uri(result.LatestReleaseUrl));
    }

    private void SetActiveButton(Button activeButton)
    {
        SetButtonState(PreviewButton, activeButton == PreviewButton);
        SetButtonState(SettingsButton, activeButton == SettingsButton);
        SetButtonState(FavoritesButton, activeButton == FavoritesButton);
        SetButtonState(LibraryButton, activeButton == LibraryButton);
        SetButtonState(AboutButton, activeButton == AboutButton);
    }

    private static void SetButtonState(Button button, bool isActive)
    {
        button.Background = isActive ? ActiveNavBrush : InactiveNavBrush;
        button.Foreground = isActive
            ? new SolidColorBrush(Microsoft.UI.Colors.White)
            : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
    }

    private void ApplyNavigationLabels()
    {
        ApplyButtonLabel(PreviewButton, "Calendar");
        ApplyButtonLabel(SettingsButton, "Settings");
        ApplyButtonLabel(FavoritesButton, "Favorites");
        ApplyButtonLabel(LibraryButton, "Library");
        ApplyButtonLabel(AboutButton, "About");
    }

    private static void ApplyButtonLabel(Button button, string text)
    {
        var localizedText = AppStrings.Get(text);
        ToolTipService.SetToolTip(button, localizedText);
        AutomationProperties.SetName(button, localizedText);
    }

    private void NotifyCalendarHostReturned()
    {
        if (ContentFrame.Content is MainPage mainPage)
            mainPage.NotifyHostReturnedToCalendar();
    }
}
