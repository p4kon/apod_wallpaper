using System;

namespace apod_wallpaper.WinUI;

internal sealed class AppVersionInfo
{
    public Version? Version { get; set; }
    public bool HasPackageIdentity { get; set; }
}
