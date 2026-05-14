using Microsoft.Win32;
using System;
using System.IO;
using Windows.ApplicationModel;

namespace apod_wallpaper.WinUI;

internal sealed class StoreStartupRegistrationService : apod_wallpaper.IStartupRegistrationService
{
    private const string RunRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string PortableAppName = "APODWallpaper.WinUI";
    private const string LegacyWinFormsAppName = "apod_wallpaper";

    public void SetStartWithWindows(bool enabled)
    {
        if (HasPackageIdentity())
        {
            // Packaged startup requires a StartupTask declaration and async user consent.
            // Until the Store package path is finalized, avoid writing an unpackaged Run key
            // from an MSIX context.
            return;
        }

        SetPortableStartup(enabled);
    }

    private static void SetPortableStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunRegistryPath, writable: true);

        if (key == null)
            throw new InvalidOperationException("Unable to open the current user's Windows startup registry key.");

        if (enabled)
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
                executablePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;

            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
                throw new InvalidOperationException("Unable to resolve the current executable path for Windows startup registration.");

            key.SetValue(PortableAppName, "\"" + executablePath + "\"", RegistryValueKind.String);
            key.DeleteValue(LegacyWinFormsAppName, throwOnMissingValue: false);
        }
        else
        {
            key.DeleteValue(PortableAppName, throwOnMissingValue: false);
        }
    }

    private static bool HasPackageIdentity()
    {
        try
        {
            _ = Package.Current.Id.FullName;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
