using System;

namespace apod_wallpaper
{
    public interface IApplicationSessionFacade
    {
        event EventHandler<WallpaperAppliedEventArgs> WallpaperApplied;

        OperationResult<ApplicationSettingsSnapshot> Initialize();
        OperationResult Shutdown();
    }
}
