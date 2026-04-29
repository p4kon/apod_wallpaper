using System;

namespace apod_wallpaper
{
    public interface IApplicationSessionFacade
    {
        OperationResult<ApplicationSettingsSnapshot> Initialize();
        OperationResult<IEventSubscription> SubscribeWallpaperApplied(EventHandler<WallpaperAppliedEventArgs> handler);
        OperationResult Shutdown();
    }
}
