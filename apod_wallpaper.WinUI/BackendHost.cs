using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace apod_wallpaper.WinUI;

internal sealed class BackendHost : IDisposable
{
    public BackendHost()
    {
        ConfigureStorageLayout();

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

    public async Task<apod_wallpaper.OperationResult> ShutdownAsync()
    {
        if (Backend != null)
            return await Backend.ShutdownAsync();

        return apod_wallpaper.OperationResult.Success();
    }

    public void Dispose()
    {
        if (Backend is IDisposable disposable)
            disposable.Dispose();
    }

    private static void ConfigureStorageLayout()
    {
        try
        {
            var localFolderPath = ApplicationData.Current.LocalFolder.Path;
            apod_wallpaper.ApplicationStorageLayout.Configure(
                apod_wallpaper.ApplicationStorageMode.Store,
                localFolderPath);
        }
        catch
        {
            var portableDataPath = Path.Combine(AppContext.BaseDirectory, "data");
            apod_wallpaper.ApplicationStorageLayout.Configure(
                apod_wallpaper.ApplicationStorageMode.Portable,
                portableDataPath);
        }
    }
}
