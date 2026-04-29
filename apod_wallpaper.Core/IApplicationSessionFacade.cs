using System;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    public interface IApplicationSessionFacade
    {
        Task<OperationResult<ApplicationSettingsSnapshot>> InitializeAsync();
        Task<OperationResult<IEventSubscription>> SubscribeWallpaperAppliedAsync(EventHandler<WallpaperAppliedEventArgs> handler);
        Task<OperationResult> ShutdownAsync();
    }
}
