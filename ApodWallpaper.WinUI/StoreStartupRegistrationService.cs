namespace ApodWallpaper.WinUI;

internal sealed class StoreStartupRegistrationService : apod_wallpaper.IStartupRegistrationService
{
    public void SetStartWithWindows(bool enabled)
    {
        // StartupTask integration is intentionally deferred to the Store tech-debt phase.
        // The composition root already depends on the correct abstraction, so the real
        // packaged implementation can be swapped in later without touching Core.
    }
}
