using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel;
using Windows.System;

namespace apod_wallpaper.WinUI;

public sealed partial class AboutPage : Page
{
    private static readonly Uri ProjectRepositoryUri = new("https://github.com/p4kon/apod_wallpaper");
    private static readonly Uri OfficialWebsiteUri = new("https://apod_wallpaper.p4kon.com");
    private static readonly Uri SupportUri = new("mailto:p4kon1@gmail.com?subject=APOD%20Wallpaper%20support");
    private static readonly Uri PrivacyPolicyUri = new("https://apod_wallpaper.p4kon.com/privacy.html");
    private static readonly Uri LicenseUri = new("https://github.com/p4kon/apod_wallpaper/blob/main/LICENSE");
    private static readonly Uri ThirdPartyNoticesUri = new("https://github.com/p4kon/apod_wallpaper/blob/main/THIRD_PARTY_NOTICES.md");
    private static readonly Uri NasaApodUri = new("https://apod.nasa.gov/apod/");
    private static readonly Uri NasaApiUri = new("https://api.nasa.gov/");

    public AboutPage()
    {
        InitializeComponent();
        LocalizationHelper.ApplyTo(this);
        PopulateAppInfo();
    }

    private void PopulateAppInfo()
    {
        try
        {
            var package = Package.Current;
            var version = package.Id.Version;
            ApplyVersion(version.Major, version.Minor, version.Build, version.Revision);

            AboutStatusBar.Severity = InfoBarSeverity.Informational;
            AboutStatusBar.Title = AppStrings.Get("Product info ready");
            AboutStatusBar.Message = AppStrings.Get("Repository, support, licensing, and runtime service credits are available from this screen.");
        }
        catch
        {
            var version = ResolveUnpackagedVersion();
            if (version != null)
                ApplyVersion(version.Major, version.Minor, version.Build, version.Revision);
            else
                VersionTextBlock.Text = AppStrings.Get("Unknown");
            PackageTextBlock.Text = AppStrings.Get("Package identity unavailable in this launch context.");

            AboutStatusBar.Severity = InfoBarSeverity.Warning;
            AboutStatusBar.Title = AppStrings.Get("Running without package identity");
            AboutStatusBar.Message = AppStrings.Get("Version info was resolved from the assembly because packaged identity was unavailable.");
        }
    }

    private void ApplyVersion(int major, int minor, int build, int revision)
    {
        VersionTextBlock.Text = AppStrings.Format("Version {0}.{1}.{2} ({3})", major, minor, build, Environment.Is64BitProcess ? "64-bit" : "32-bit");
        PackageTextBlock.Text = AppStrings.Format("Build {0}.{1}", build, revision);
    }

    private static Version? ResolveUnpackagedVersion()
    {
        return TryReadAppManifestVersion()
            ?? TryReadExecutableVersion()
            ?? Assembly.GetExecutingAssembly().GetName().Version;
    }

    private static Version? TryReadAppManifestVersion()
    {
        var baseDirectory = AppContext.BaseDirectory;
        foreach (var fileName in new[] { "AppxManifest.xml", "Package.appxmanifest" })
        {
            var path = Path.Combine(baseDirectory, fileName);
            if (!File.Exists(path))
                continue;

            try
            {
                var document = XDocument.Load(path);
                var ns = document.Root?.Name.Namespace ?? XNamespace.None;
                var versionText = document.Root?.Element(ns + "Identity")?.Attribute("Version")?.Value;
                if (Version.TryParse(versionText, out var version))
                    return version;
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static Version? TryReadExecutableVersion()
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
                return null;

            var versionInfo = FileVersionInfo.GetVersionInfo(executablePath);
            return Version.TryParse(versionInfo.ProductVersion, out var version)
                ? version
                : null;
        }
        catch
        {
            return null;
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
