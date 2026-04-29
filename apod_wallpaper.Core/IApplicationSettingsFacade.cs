using System;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    public interface IApplicationSettingsFacade
    {
        OperationResult<ApplicationSettingsSnapshot> GetSettings();
        OperationResult<ApplicationSettingsSnapshot> SaveSettings(ApplicationSettingsSnapshot settings);
        OperationResult<string> UpdateSessionImagesDirectory(string path);
        OperationResult<string> GetEffectiveImagesDirectory();
        OperationResult<string> EnsureEffectiveImagesDirectory();
        OperationResult<ApiKeyValidationState> GetApiKeyValidationState();
        Task<OperationResult> RefreshLocalImageIndexAsync();
        OperationResult<DateTime> GetPreferredDisplayDate();
        OperationResult<WallpaperStyle> GetSelectedWallpaperStyle();
        OperationResult<bool> ShouldApplyOnTrayDoubleClick();
    }
}
