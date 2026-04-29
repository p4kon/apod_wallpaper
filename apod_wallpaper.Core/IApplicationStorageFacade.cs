using System.Threading.Tasks;

namespace apod_wallpaper
{
    public interface IApplicationStorageFacade
    {
        Task<OperationResult<ApplicationStoragePaths>> GetStoragePathsAsync();
        Task<OperationResult<ApplicationStoragePaths>> EnsureStorageLayoutAsync();
    }
}
