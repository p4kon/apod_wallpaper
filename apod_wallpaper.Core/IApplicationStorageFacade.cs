namespace apod_wallpaper
{
    public interface IApplicationStorageFacade
    {
        OperationResult<ApplicationStoragePaths> GetStoragePaths();
        OperationResult<ApplicationStoragePaths> EnsureStorageLayout();
    }
}
