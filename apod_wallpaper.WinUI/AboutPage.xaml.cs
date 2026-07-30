using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace apod_wallpaper.WinUI;

public sealed partial class AboutPage : Page
{
    private enum UpdateStatusVisualState
    {
        None,
        Checking,
        UpdateAvailable,
        UpToDate,
        CouldNotCheck,
    }

    private static readonly Uri ProjectRepositoryUri = new("https://github.com/p4kon/apod_wallpaper");
    private static readonly Uri OfficialWebsiteUri = new("https://apod_wallpaper.p4kon.com");
    private static readonly Uri SupportUri = new("mailto:p4kon1@gmail.com?subject=APOD%20Wallpaper%20support");
    private static readonly Uri PrivacyPolicyUri = new("https://apod_wallpaper.p4kon.com/privacy.html");
    private static readonly Uri LicenseUri = new("https://github.com/p4kon/apod_wallpaper/blob/main/LICENSE");
    private static readonly Uri ThirdPartyNoticesUri = new("https://github.com/p4kon/apod_wallpaper/blob/main/THIRD_PARTY_NOTICES.md");
    private static readonly Uri NasaApodUri = new("https://apod.nasa.gov/apod/");
    private static readonly Uri NasaApiUri = new("https://api.nasa.gov/");
    private readonly DispatcherTimer _statusDismissTimer = new();
    private AboutPageArguments? _arguments;
    private UpdateStatusVisualState _updateStatusVisualState = UpdateStatusVisualState.None;

    public AboutPage()
    {
        InitializeComponent();
        LocalizationHelper.ApplyTo(this);
        PopulateAppInfo();
        _statusDismissTimer.Interval = TimeSpan.FromSeconds(5);
        _statusDismissTimer.Tick += StatusDismissTimer_Tick;
        AppStrings.LanguageChanged += AppStrings_LanguageChanged;
        Loaded += AboutPage_Loaded;
        Unloaded += AboutPage_Unloaded;
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _arguments = e.Parameter as AboutPageArguments;
        _ = RefreshUpdateStatusFromBackendAsync();
    }

    private void AppStrings_LanguageChanged(object? sender, EventArgs e)
    {
        LocalizationHelper.ApplyTo(this);
        PopulateAppInfo();
        _ = RefreshUpdateStatusFromBackendAsync();
    }

    private void AboutPage_Unloaded(object sender, RoutedEventArgs e)
    {
        AppStrings.LanguageChanged -= AppStrings_LanguageChanged;
        _statusDismissTimer.Stop();
        _statusDismissTimer.Tick -= StatusDismissTimer_Tick;
        Loaded -= AboutPage_Loaded;
        Unloaded -= AboutPage_Unloaded;
    }

    private void AboutPage_Loaded(object sender, RoutedEventArgs e)
    {
        LocalizationHelper.ApplyTo(this);
        PopulateAppInfo();
        _ = RefreshUpdateStatusFromBackendAsync();
    }

    private void PopulateAppInfo()
    {
        try
        {
            var info = AppVersionResolver.Resolve();
            if (info.Version == null)
                VersionTextBlock.Text = AppStrings.Get("Unknown");
            else
                ApplyVersion(info.Version.Major, info.Version.Minor, info.Version.Build, info.Version.Revision);

            if (info.HasPackageIdentity)
            {
                AboutStatusBar.Severity = InfoBarSeverity.Informational;
                AboutStatusBar.Title = AppStrings.Get("Product info ready");
                AboutStatusBar.Message = AppStrings.Get("Repository, support, licensing, and runtime service credits are available from this screen.");
            }
            else
            {
                PackageTextBlock.Text = AppStrings.Get("Package identity unavailable in this launch context.");
                AboutStatusBar.Severity = InfoBarSeverity.Warning;
                AboutStatusBar.Title = AppStrings.Get("Running without package identity");
                AboutStatusBar.Message = AppStrings.Get("Version info was resolved from the assembly because packaged identity was unavailable.");
            }
        }
        catch (Exception ex)
        {
            VersionTextBlock.Text = AppStrings.Get("Unknown");
            AboutStatusBar.Severity = InfoBarSeverity.Warning;
            AboutStatusBar.Title = AppStrings.Get("Product info");
            AboutStatusBar.Message = ex.Message;
        }
    }

    private void ApplyVersion(int major, int minor, int build, int revision)
    {
        VersionTextBlock.Text = AppStrings.Format("Version {0}.{1}.{2} ({3})", major, minor, build, Environment.Is64BitProcess ? "64-bit" : "32-bit");
        PackageTextBlock.Text = AppStrings.Format("Build {0}.{1}", build, revision);
    }

    private async void ProjectRepositoryButton_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(ProjectRepositoryUri);
    }

    private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_arguments == null)
            return;

        CheckUpdatesButton.IsEnabled = false;
        ShowUpdateStatus(AppStrings.Get("Checking..."), UpdateStatusVisualState.Checking, transient: false);
        ShowAboutStatus(
            InfoBarSeverity.Informational,
            AppStrings.Get("Checking for updates"),
            AppStrings.Get("Checking GitHub Releases for the latest APOD Wallpaper version."),
            autoDismiss: false);

        try
        {
            var currentVersion = AppVersionResolver.ResolveCurrentVersionText();
            var result = await _arguments.BackendHost.Backend.CheckForUpdatesAsync(currentVersion, forceCheck: true, automatic: false);
            if (!result.Succeeded || result.Value == null)
            {
                if (!await TryShowPersistentUpdateStatusAsync())
                    ShowUpdateStatus(AppStrings.Get("Could not check updates"), UpdateStatusVisualState.CouldNotCheck, transient: true);

                ShowAboutStatus(
                    InfoBarSeverity.Error,
                    AppStrings.Get("Could not check for updates"),
                    AppStrings.GetBackendMessageOrDefault(result.Error?.Message, "Could not check for updates."),
                    autoDismiss: true);
                return;
            }

            await HandleManualUpdateCheckResultAsync(result.Value);
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private async System.Threading.Tasks.Task HandleManualUpdateCheckResultAsync(apod_wallpaper.UpdateCheckResult result)
    {
        if (result.Status == apod_wallpaper.UpdateCheckStatus.UpdateAvailable)
        {
            ShowUpdateStatus(AppStrings.Format("Update available: {0}", result.LatestVersion), UpdateStatusVisualState.UpdateAvailable, transient: false);
            ShowAboutStatus(
                InfoBarSeverity.Warning,
                AppStrings.Get("Update available"),
                AppStrings.Format("Version {0} is available.", result.LatestVersion),
                autoDismiss: false);

            var choice = await UpdateNotificationDialog.ShowAsync(XamlRoot, result, includeDoNotRemind: false);
            if (choice == UpdateDialogChoice.OpenRelease)
                await OpenReleaseAsync(result);

            return;
        }

        if (result.Status == apod_wallpaper.UpdateCheckStatus.UpToDate)
        {
            ShowUpdateStatus(AppStrings.Get("You are up to date"), UpdateStatusVisualState.UpToDate, transient: true);
            ShowAboutStatus(
                InfoBarSeverity.Success,
                AppStrings.Get("APOD Wallpaper is up to date"),
                AppStrings.Format("You are running version {0}.", result.CurrentVersion),
                autoDismiss: true);
            return;
        }

        if (!await TryShowPersistentUpdateStatusAsync())
            ShowUpdateStatus(AppStrings.Get("Could not check updates"), UpdateStatusVisualState.CouldNotCheck, transient: true);
        ShowAboutStatus(
            InfoBarSeverity.Warning,
            AppStrings.Get("Could not check for updates"),
            AppStrings.GetBackendMessageOrDefault(result.Message, "Could not check for updates."),
            autoDismiss: true);
    }

    internal async System.Threading.Tasks.Task RefreshUpdateStatusFromBackendAsync()
    {
        await TryShowPersistentUpdateStatusAsync(hideStalePersistentStatus: true);
    }

    private async System.Threading.Tasks.Task<bool> TryShowPersistentUpdateStatusAsync(bool hideStalePersistentStatus = false)
    {
        if (_arguments == null)
            return false;

        var settingsResult = await _arguments.BackendHost.Backend.GetSettingsAsync();
        if (!settingsResult.Succeeded || settingsResult.Value == null)
            return false;

        var currentVersion = AppVersionResolver.ResolveCurrentVersionText();
        var cachedStatus = apod_wallpaper.UpdateReminderPolicy.GetCachedUpdateAvailability(settingsResult.Value, currentVersion);
        if (cachedStatus.Kind == apod_wallpaper.CachedUpdateAvailabilityKind.UpdateAvailable)
        {
            ShowUpdateStatus(
                AppStrings.Format("Update available: {0}", cachedStatus.LatestVersion),
                UpdateStatusVisualState.UpdateAvailable,
                transient: false);
            return true;
        }

        if (hideStalePersistentStatus && _updateStatusVisualState == UpdateStatusVisualState.UpdateAvailable)
            HideUpdateStatus();

        return false;
    }

    private void ShowUpdateStatus(string text, UpdateStatusVisualState visualState, bool transient)
    {
        _updateStatusVisualState = visualState;
        UpdateStatusTextBlock.Text = text;
        ToolTipService.SetToolTip(UpdateStatusTextBlock, text);
        UpdateStatusTextBlock.Foreground = ResolveUpdateStatusBrush(visualState);
        UpdateStatusTextBlock.Visibility = Visibility.Visible;
        if (transient)
        {
            _statusDismissTimer.Stop();
            _statusDismissTimer.Start();
        }
    }

    private Brush ResolveUpdateStatusBrush(UpdateStatusVisualState visualState)
    {
        var resourceKey = visualState switch
        {
            UpdateStatusVisualState.Checking => "UpdateStatusCheckingBrush",
            UpdateStatusVisualState.UpdateAvailable => "UpdateStatusAvailableBrush",
            UpdateStatusVisualState.UpToDate => "UpdateStatusUpToDateBrush",
            UpdateStatusVisualState.CouldNotCheck => "UpdateStatusCouldNotCheckBrush",
            _ => "TextFillColorSecondaryBrush",
        };

        return Resources.TryGetValue(resourceKey, out var brush) && brush is Brush typedBrush
            ? typedBrush
            : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
    }

    private void ShowAboutStatus(InfoBarSeverity severity, string title, string message, bool autoDismiss)
    {
        _statusDismissTimer.Stop();
        AboutStatusBar.Severity = severity;
        AboutStatusBar.Title = title;
        AboutStatusBar.Message = message;
        AboutStatusBar.Visibility = Visibility.Visible;
        if (autoDismiss)
            _statusDismissTimer.Start();
    }

    private void HideAboutStatus()
    {
        _statusDismissTimer.Stop();
        AboutStatusBar.Visibility = Visibility.Collapsed;
        if (_updateStatusVisualState is UpdateStatusVisualState.UpToDate or UpdateStatusVisualState.CouldNotCheck)
            HideUpdateStatus();
    }

    private void HideUpdateStatus()
    {
        _updateStatusVisualState = UpdateStatusVisualState.None;
        UpdateStatusTextBlock.Text = string.Empty;
        ToolTipService.SetToolTip(UpdateStatusTextBlock, null);
        UpdateStatusTextBlock.Visibility = Visibility.Collapsed;
    }

    private void StatusDismissTimer_Tick(object? sender, object e)
    {
        HideAboutStatus();
    }

    private void AboutRoot_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (AboutStatusBar.Visibility == Visibility.Visible)
            HideAboutStatus();
    }

    private static async System.Threading.Tasks.Task OpenReleaseAsync(apod_wallpaper.UpdateCheckResult result)
    {
        if (string.IsNullOrWhiteSpace(result.LatestReleaseUrl))
            await Launcher.LaunchUriAsync(ProjectRepositoryUri);
        else
            await Launcher.LaunchUriAsync(new Uri(result.LatestReleaseUrl));
    }

    private async void SupportButton_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(SupportUri);
    }

    private async void OfficialWebsiteButton_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(OfficialWebsiteUri);
    }

    private async void PrivacyButton_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(PrivacyPolicyUri);
    }

    private async void LicenseButton_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(LicenseUri);
    }

    private async void ThirdPartyNoticesButton_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(ThirdPartyNoticesUri);
    }
}
