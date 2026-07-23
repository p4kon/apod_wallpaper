using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    public interface IApodFavoritesFacade
    {
        Task<OperationResult<IReadOnlyList<DateTime>>> GetFavoriteDatesAsync();
        Task<OperationResult<IReadOnlyList<FavoriteApodItem>>> GetFavoriteApodsAsync();
        Task<OperationResult<bool>> IsFavoriteAsync(DateTime date);
        Task<OperationResult<bool>> SetFavoriteAsync(DateTime date, bool isFavorite);
    }
}
