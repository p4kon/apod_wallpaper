using System.Threading.Tasks;

namespace apod_wallpaper
{
    public interface IRandomApodFacade
    {
        Task<OperationResult<RandomApodResult>> PickRandomApodDateAsync(string source, bool includeDeepArchive);
    }
}
