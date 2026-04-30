using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace ApodWallpaper.WinUI;

internal sealed class BackendHost : IDisposable
{
    public BackendHost()
    {
        var localFolderPath = ApplicationData.Current.LocalFolder.Path;
        apod_wallpaper.ApplicationStorageLayout.Configure(
            apod_wallpaper.ApplicationStorageMode.Store,
            localFolderPath);

        SettingsStore = new apod_wallpaper.JsonSettingsStore();
        SecretStore = new apod_wallpaper.DpapiUserSecretStore();
        StartupRegistrationService = new StoreStartupRegistrationService();
        Backend = new apod_wallpaper.ApplicationController(
            SettingsStore,
            SecretStore,
            StartupRegistrationService);
    }

    public apod_wallpaper.IApplicationSettingsStore SettingsStore { get; }

    public apod_wallpaper.IUserSecretStore SecretStore { get; }

    public apod_wallpaper.IStartupRegistrationService StartupRegistrationService { get; }

    public apod_wallpaper.IApplicationBackendFacade Backend { get; }

    public Task<apod_wallpaper.OperationResult<apod_wallpaper.ApplicationSettingsSnapshot>> InitializeAsync()
    {
        return Backend.InitializeAsync();
    }

    public Task<apod_wallpaper.OperationResult<apod_wallpaper.ApplicationInitialStateSnapshot>> GetInitialStateAsync()
    {
        return Backend.GetInitialStateAsync();
    }

    public async Task ShutdownAsync()
    {
        if (Backend != null)
            await Backend.ShutdownAsync();
    }

    public void Dispose()
    {
        if (Backend is IDisposable disposable)
            disposable.Dispose();
    }
}
