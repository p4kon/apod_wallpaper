using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using Windows.ApplicationModel;

namespace apod_wallpaper.WinUI;

internal static class AppVersionResolver
{
    public static AppVersionInfo Resolve()
    {
        try
        {
            var package = Package.Current;
            var version = package.Id.Version;
            return new AppVersionInfo
            {
                Version = new Version(version.Major, version.Minor, version.Build, version.Revision),
                HasPackageIdentity = true,
            };
        }
        catch
        {
            return new AppVersionInfo
            {
                Version = ResolveUnpackagedVersion(),
                HasPackageIdentity = false,
            };
        }
    }

    public static string ResolveCurrentVersionText()
    {
        var version = Resolve().Version;
        if (version == null)
            return "0.0.0";

        return version.Build > 0
            ? version.ToString(3)
            : version.ToString(2);
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
            var productVersion = versionInfo.ProductVersion?.Split('+')[0];
            if (Version.TryParse(productVersion, out var version))
                return version;

            return Version.TryParse(versionInfo.FileVersion, out version)
                ? version
                : null;
        }
        catch
        {
            return null;
        }
    }
}
