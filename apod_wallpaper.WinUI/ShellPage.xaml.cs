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
    private System.Threading.Tasks.Task? _automaticUpdateCheckTask;
    private bool _updateDialogOpen;
    private bool _updateReminderShownThisSession;
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
        await TryRunAutomaticUpdateReminderAsync(UpdateCheckTrigger.Startup);
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
        _ = TryRunAutomaticUpdateReminderAsync(UpdateCheckTrigger.WindowActivated);
    }

    internal void NotifyRestoredFromTray()
    {
        NotifyCalendarHostReturned();
        _ = TryRunAutomaticUpdateReminderAsync(UpdateCheckTrigger.RestoredFromTray);
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

    private System.Threading.Tasks.Task TryRunAutomaticUpdateReminderAsync(UpdateCheckTrigger trigger)
    {
        if (_automaticUpdateCheckTask != null && !_automaticUpdateCheckTask.IsCompleted)
            return _automaticUpdateCheckTask;

        _automaticUpdateCheckTask = RunAutomaticUpdateReminderAsync(trigger);
        return _automaticUpdateCheckTask;
    }

    private async System.Threading.Tasks.Task RunAutomaticUpdateReminderAsync(UpdateCheckTrigger trigger)
    {
        if (_arguments == null || _updateDialogOpen || _updateReminderShownThisSession)
            return;

        var settingsResult = await _arguments.BackendHost.Backend.GetSettingsAsync();
        if (!settingsResult.Succeeded || settingsResult.Value == null)
            return;

        var settings = settingsResult.Value;
        if (!apod_wallpaper.UpdateReminderPolicy.ShouldRunAutomaticCheck(settings, DateTime.UtcNow))
            return;

        var currentVersion = AppVersionResolver.ResolveCurrentVersionText();
        var checkResult = await _arguments.BackendHost.Backend.CheckForUpdatesAsync(currentVersion, forceCheck: false, automatic: true);
        if (!checkResult.Succeeded || checkResult.Value == null || checkResult.Value.Status != apod_wallpaper.UpdateCheckStatus.UpdateAvailable)
            return;

        var latestSettingsResult = await _arguments.BackendHost.Backend.GetSettingsAsync();
        var latestSettings = latestSettingsResult.Succeeded && latestSettingsResult.Value != null
            ? latestSettingsResult.Value
            : settings;
        if (!apod_wallpaper.UpdateReminderPolicy.ShouldShowReminder(latestSettings, checkResult.Value.LatestVersion, currentVersion, DateTime.UtcNow, _updateReminderShownThisSession))
            return;

        _updateDialogOpen = true;
        UpdateDialogChoice choice;
        try
        {
            choice = await UpdateNotificationDialog.ShowAsync(XamlRoot, checkResult.Value, includeDoNotRemind: true);
        }
        finally
        {
            _updateDialogOpen = false;
        }

        _updateReminderShownThisSession = true;
        if (choice == UpdateDialogChoice.OpenRelease)
        {
            await StoreUpdateReminderChoiceAsync(checkResult.Value, suppressAutomaticReminders: false);
            await OpenReleaseAsync(checkResult.Value);
            return;
        }

        if (choice == UpdateDialogChoice.DoNotRemind)
            await StoreUpdateReminderChoiceAsync(checkResult.Value, suppressAutomaticReminders: true);
        else
            await StoreUpdateReminderChoiceAsync(checkResult.Value, suppressAutomaticReminders: false);
    }

    private async System.Threading.Tasks.Task StoreUpdateReminderChoiceAsync(apod_wallpaper.UpdateCheckResult result, bool suppressAutomaticReminders)
    {
        if (_arguments == null)
            return;

        var settingsResult = await _arguments.BackendHost.Backend.GetSettingsAsync();
        if (!settingsResult.Succeeded || settingsResult.Value == null)
            return;

        var settings = settingsResult.Value.Clone();
        settings.LastUpdateReminderShownUtc = apod_wallpaper.UpdateReminderPolicy.FormatUtc(DateTime.UtcNow);
        settings.LastUpdateReminderVersion = result.LatestVersion;
        if (!string.IsNullOrWhiteSpace(result.LatestVersion))
            settings.LastKnownLatestVersion = result.LatestVersion;
        if (!string.IsNullOrWhiteSpace(result.LatestReleaseUrl))
            settings.LastKnownLatestReleaseUrl = result.LatestReleaseUrl;
        if (suppressAutomaticReminders)
        {
            settings.AutoCheckUpdatesEnabled = false;
            settings.SuppressAutomaticUpdateReminder = true;
        }
        var saveResult = await _arguments.BackendHost.Backend.SaveSettingsAsync(settings);
        if (saveResult.Succeeded && suppressAutomaticReminders)
            await RefreshSettingsPageIfVisibleAsync();
    }

    private async System.Threading.Tasks.Task RefreshSettingsPageIfVisibleAsync()
    {
        if (ContentFrame.Content is SettingsPage settingsPage)
            await settingsPage.RefreshSettingsFromBackendAsync();
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
        DispatcherQueue.TryEnqueue(() =>
        {
            if (ContentFrame.Content is MainPage mainPage)
                mainPage.NotifyHostReturnedToCalendar();
        });
    }
}

internal enum UpdateCheckTrigger
{
    Startup,
    WindowActivated,
    RestoredFromTray,
}
