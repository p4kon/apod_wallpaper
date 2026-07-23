using System.Threading.Tasks;

namespace apod_wallpaper
{
    public interface IUpdateCheckFacade
    {
        Task<OperationResult<UpdateCheckResult>> CheckForUpdatesAsync(string currentVersion, bool forceCheck, bool automatic);
    }
}
