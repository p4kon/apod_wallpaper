using Microsoft.Win32;
using System;
using System.IO;
using System.Reflection;
using Windows.ApplicationModel;
using Windows.Foundation;

namespace apod_wallpaper.WinUI;

internal sealed class StoreStartupRegistrationService : apod_wallpaper.IStartupRegistrationService
{
    private const string PackagedStartupTaskId = "APODWallpaperStartupTask";
    private const string RunRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupApprovedRunRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string PortableAppName = "APODWallpaper.WinUI";
    private const string LegacyWinFormsAppName = "apod_wallpaper";
    private static readonly string[] KnownStartupValueNames =
    {
        PortableAppName,
        "apod_wallpaper.WinUI",
        "ApodWallpaper.WinUI",
        "APODWallpaper",
        "APOD Wallpaper",
        LegacyWinFormsAppName
    };

    private static readonly string[] KnownExecutableNames =
    {
        "apod_wallpaper.WinUI.exe",
        "ApodWallpaper.WinUI.exe",
        "APODWallpaper.WinUI.exe",
        "apod_wallpaper.exe",
        "ApodWallpaper.exe"
    };

    public void SetStartWithWindows(bool enabled)
    {
        if (HasPackageIdentity())
        {
            SetPackagedStartup(enabled);
            return;
        }

        SetPortableStartup(enabled);
    }

    private static void SetPackagedStartup(bool enabled)
    {
        var startupTask = StartupTask.GetAsync(PackagedStartupTaskId).AsTask().GetAwaiter().GetResult();
        if (enabled)
        {
            if (startupTask.State == StartupTaskState.Disabled)
            {
                var state = startupTask.RequestEnableAsync().AsTask().GetAwaiter().GetResult();
                if (state != StartupTaskState.Enabled)
                    throw new InvalidOperationException("Windows did not enable the packaged startup task. Current state: " + state + ".");
            }

            if (startupTask.State == StartupTaskState.Enabled)
                return;

            throw new InvalidOperationException("Packaged startup task cannot be enabled from its current state: " + startupTask.State + ".");
        }

        if (startupTask.State == StartupTaskState.Enabled)
            startupTask.Disable();
    }

    private static void SetPortableStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunRegistryPath, writable: true);

        if (key == null)
            throw new InvalidOperationException("Unable to open the current user's Windows startup registry key.");

        DeleteKnownStartupEntries(key);
        DeleteKnownStartupApprovalEntries();

        if (enabled)
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
                executablePath = Assembly.GetEntryAssembly()?.Location;

            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
                throw new InvalidOperationException("Unable to resolve the current executable path for Windows startup registration.");

            key.SetValue(PortableAppName, "\"" + executablePath + "\"", RegistryValueKind.String);
        }
    }

    private static void DeleteKnownStartupApprovalEntries()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupApprovedRunRegistryPath, writable: true);
        if (key == null)
            return;

        foreach (var valueName in key.GetValueNames())
        {
            if (IsKnownStartupValueName(valueName))
                key.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }

    private static void DeleteKnownStartupEntries(RegistryKey key)
    {
        foreach (var valueName in key.GetValueNames())
        {
            if (IsKnownStartupValueName(valueName) || IsKnownStartupCommand(key.GetValue(valueName) as string))
                key.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }

    private static bool IsKnownStartupValueName(string valueName)
    {
        foreach (var knownName in KnownStartupValueNames)
        {
            if (string.Equals(valueName, knownName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsKnownStartupCommand(string? command)
    {
        var executablePath = TryExtractExecutablePath(command);
        if (string.IsNullOrWhiteSpace(executablePath))
            return false;

        var executableName = Path.GetFileName(executablePath);
        foreach (var knownExecutableName in KnownExecutableNames)
        {
            if (string.Equals(executableName, knownExecutableName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string? TryExtractExecutablePath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return null;

        var trimmedCommand = command.Trim();
        if (trimmedCommand.Length == 0)
            return null;

        if (trimmedCommand[0] == '"')
        {
            var closingQuoteIndex = trimmedCommand.IndexOf('"', 1);
            return closingQuoteIndex > 1
                ? trimmedCommand.Substring(1, closingQuoteIndex - 1)
                : null;
        }

        var executableExtensionIndex = trimmedCommand.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (executableExtensionIndex >= 0)
            return trimmedCommand.Substring(0, executableExtensionIndex + 4).Trim();

        var firstSpaceIndex = trimmedCommand.IndexOf(' ');
        return firstSpaceIndex > 0
            ? trimmedCommand.Substring(0, firstSpaceIndex).Trim()
            : trimmedCommand;
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
