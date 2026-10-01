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

    internal void NotifyWindowDeactivated()
    {
        if (ContentFrame.Content is MainPage mainPage)
            mainPage.NotifyHostLeftCalendar();
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
        LogUpdateReminderDiagnostic(trigger, "trigger");
        if (_arguments == null)
        {
            LogUpdateReminderDiagnostic(trigger, "skip:no-arguments");
            return;
        }

        if (_updateDialogOpen)
        {
            LogUpdateReminderDiagnostic(trigger, "skip:dialog-open");
            return;
        }

        if (_updateReminderShownThisSession)
        {
            LogUpdateReminderDiagnostic(trigger, "skip:shown-this-session");
            return;
        }

        var settingsResult = await _arguments.BackendHost.Backend.GetSettingsAsync();
        if (!settingsResult.Succeeded || settingsResult.Value == null)
        {
            LogUpdateReminderDiagnostic(trigger, "skip:settings-unavailable");
            return;
        }

        var settings = settingsResult.Value;
        if (!apod_wallpaper.UpdateReminderPolicy.ShouldRunAutomaticCheck(settings, DateTime.UtcNow))
        {
            LogUpdateReminderDiagnostic(trigger, BuildAutomaticSkipReason(settings), settings);
            return;
        }

        var currentVersion = AppVersionResolver.ResolveCurrentVersionText();
        LogUpdateReminderDiagnostic(trigger, "network-check:start", settings, currentVersion: currentVersion);
        var checkResult = await _arguments.BackendHost.Backend.CheckForUpdatesAsync(currentVersion, forceCheck: false, automatic: true);
        if (!checkResult.Succeeded || checkResult.Value == null)
        {
            LogUpdateReminderDiagnostic(trigger, "network-check:failed", settings, currentVersion: currentVersion);
            return;
        }

        LogUpdateReminderDiagnostic(trigger, "network-check:complete", settings, checkResult.Value, currentVersion);
        await RefreshAboutPageUpdateStatusIfVisibleAsync();
        if (checkResult.Value.Status != apod_wallpaper.UpdateCheckStatus.UpdateAvailable)
        {
            LogUpdateReminderDiagnostic(trigger, "skip:no-update", settings, checkResult.Value, currentVersion);
            return;
        }

        var latestSettingsResult = await _arguments.BackendHost.Backend.GetSettingsAsync();
        var latestSettings = latestSettingsResult.Succeeded && latestSettingsResult.Value != null
            ? latestSettingsResult.Value
            : settings;
        if (!apod_wallpaper.UpdateReminderPolicy.ShouldShowReminder(latestSettings, checkResult.Value.LatestVersion, currentVersion, DateTime.UtcNow, _updateReminderShownThisSession))
        {
            LogUpdateReminderDiagnostic(trigger, "skip:reminder-policy", latestSettings, checkResult.Value, currentVersion);
            return;
        }

        LogUpdateReminderDiagnostic(trigger, "modal:show", latestSettings, checkResult.Value, currentVersion);
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
        LogUpdateReminderDiagnostic(trigger, "modal:choice:" + choice, latestSettings, checkResult.Value, currentVersion);
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
        LogUpdateReminderDiagnostic(UpdateCheckTrigger.WindowActivated, "choice-save:before suppress=" + suppressAutomaticReminders, settings, result, result.CurrentVersion);
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
        if (saveResult.Succeeded)
        {
            var afterSaveResult = await _arguments.BackendHost.Backend.GetSettingsAsync();
            if (afterSaveResult.Succeeded && afterSaveResult.Value != null)
            {
                LogUpdateReminderDiagnostic(UpdateCheckTrigger.WindowActivated, "choice-save:after suppress=" + suppressAutomaticReminders, afterSaveResult.Value, result, result.CurrentVersion);
            }
        }
        if (saveResult.Succeeded && suppressAutomaticReminders)
            await RefreshSettingsPageIfVisibleAsync();
        if (saveResult.Succeeded)
            await RefreshAboutPageUpdateStatusIfVisibleAsync();
    }

    private async System.Threading.Tasks.Task RefreshAboutPageUpdateStatusIfVisibleAsync()
    {
        if (ContentFrame.Content is AboutPage aboutPage)
            await aboutPage.RefreshUpdateStatusFromBackendAsync();
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

    private static string BuildAutomaticSkipReason(apod_wallpaper.ApplicationSettingsSnapshot settings)
    {
        if (!settings.AutoCheckUpdatesEnabled)
            return "skip:auto-disabled";

        if (settings.SuppressAutomaticUpdateReminder)
            return "skip:suppressed";

        return "skip:throttled";
    }

    private static void LogUpdateReminderDiagnostic(
        UpdateCheckTrigger trigger,
        string stage,
        apod_wallpaper.ApplicationSettingsSnapshot? settings = null,
        apod_wallpaper.UpdateCheckResult? result = null,
        string? currentVersion = null)
    {
        var message = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "[UpdateReminder] trigger={0}; stage={1}; current={2}; latestKnown={3}; latestChecked={4}; releaseUrlKnown={5}; autoCheck={6}; suppress={7}; lastAutoCheck={8}; lastAutoFailed={9}; lastReminderShown={10}; lastReminderVersion={11}; status={12}",
            trigger,
            stage,
            currentVersion ?? string.Empty,
            settings?.LastKnownLatestVersion ?? string.Empty,
            result?.LatestVersion ?? string.Empty,
            !string.IsNullOrWhiteSpace(settings?.LastKnownLatestReleaseUrl ?? result?.LatestReleaseUrl),
            settings?.AutoCheckUpdatesEnabled,
            settings?.SuppressAutomaticUpdateReminder,
            settings?.LastAutomaticUpdateCheckUtc ?? string.Empty,
            settings?.LastAutomaticUpdateCheckFailedUtc ?? string.Empty,
            settings?.LastUpdateReminderShownUtc ?? string.Empty,
            settings?.LastUpdateReminderVersion ?? string.Empty,
            result?.Status.ToString() ?? string.Empty);
        System.Diagnostics.Debug.WriteLine(message);
        apod_wallpaper.AppLogger.Info(message);
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
