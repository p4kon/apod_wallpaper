using System;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel;
using Windows.System;

namespace apod_wallpaper.WinUI;

public sealed partial class AboutPage : Page
{
    private static readonly Uri ProjectRepositoryUri = new("https://github.com/p4kon/apod_wallpaper");
    private static readonly Uri OfficialWebsiteUri = new("https://github.com/p4kon/apod_wallpaper");
    private static readonly Uri SupportUri = new("mailto:p4kon1@gmail.com?subject=APOD%20Wallpaper%20support");
    private static readonly Uri PrivacyPolicyUri = new("https://github.com/p4kon/apod_wallpaper/blob/main/PRIVACY.md");
    private static readonly Uri LicenseUri = new("https://github.com/p4kon/apod_wallpaper/blob/main/LICENSE");
    private static readonly Uri ThirdPartyNoticesUri = new("https://github.com/p4kon/apod_wallpaper/blob/main/THIRD_PARTY_NOTICES.md");
    private static readonly Uri NasaApodUri = new("https://apod.nasa.gov/apod/");
    private static readonly Uri NasaApiUri = new("https://api.nasa.gov/");

    public AboutPage()
    {
        InitializeComponent();
        PopulateAppInfo();
    }

    private void PopulateAppInfo()
    {
        try
        {
            var package = Package.Current;
            var version = package.Id.Version;
            VersionTextBlock.Text = $"Version {version.Major}.{version.Minor}.{version.Build} ({(Environment.Is64BitProcess ? "64-bit" : "32-bit")})";
            PackageTextBlock.Text = $"Build {version.Build}.{version.Revision}";

            AboutStatusBar.Severity = InfoBarSeverity.Informational;
            AboutStatusBar.Title = "Product info ready";
            AboutStatusBar.Message = "Repository, support, licensing, and runtime service credits are available from this screen.";
        }
        catch
        {
            var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
            VersionTextBlock.Text = assemblyVersion != null
                ? "Version " + assemblyVersion
                : "Unknown";
            PackageTextBlock.Text = "Package identity unavailable in this launch context.";

            AboutStatusBar.Severity = InfoBarSeverity.Warning;
            AboutStatusBar.Title = "Running without package identity";
            AboutStatusBar.Message = "Version info was resolved from the assembly because packaged identity was unavailable.";
        }
    }

    private async void ProjectRepositoryButton_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(ProjectRepositoryUri);
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
