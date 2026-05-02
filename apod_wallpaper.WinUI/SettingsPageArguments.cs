using System;

namespace apod_wallpaper.WinUI;

internal sealed class SettingsPageArguments
{
    public SettingsPageArguments(
        BackendHost backendHost,
        apod_wallpaper.OperationResult<apod_wallpaper.ApplicationSettingsSnapshot> initialization,
        Action<bool> updateCloseBehavior)
    {
        BackendHost = backendHost;
        Initialization = initialization;
        UpdateCloseBehavior = updateCloseBehavior;
    }

    public BackendHost BackendHost { get; }

    public apod_wallpaper.OperationResult<apod_wallpaper.ApplicationSettingsSnapshot> Initialization { get; }

    public Action<bool> UpdateCloseBehavior { get; }
}
