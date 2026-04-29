using System;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    public interface IApplicationSettingsFacade
    {
        Task<OperationResult<ApplicationInitialStateSnapshot>> GetInitialStateAsync();
        Task<OperationResult<ApplicationSettingsSnapshot>> GetSettingsAsync();
        Task<OperationResult<ApplicationSettingsSnapshot>> SaveSettingsAsync(ApplicationSettingsSnapshot settings);
        Task<OperationResult<string>> UpdateSessionImagesDirectoryAsync(string path);
        Task<OperationResult<string>> GetEffectiveImagesDirectoryAsync();
        Task<OperationResult<string>> EnsureEffectiveImagesDirectoryAsync();
        Task<OperationResult<ApiKeyValidationState>> GetApiKeyValidationStateAsync();
        Task<OperationResult> RefreshLocalImageIndexAsync();
        Task<OperationResult<DateTime>> GetPreferredDisplayDateAsync();
        Task<OperationResult<WallpaperStyle>> GetSelectedWallpaperStyleAsync();
        Task<OperationResult<bool>> ShouldApplyOnTrayDoubleClickAsync();
    }
}
