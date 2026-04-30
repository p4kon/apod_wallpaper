namespace apod_wallpaper
{
    public interface ILegacySettingsMigrationSource
    {
        ApplicationSettingsSnapshot LoadLegacySettings();
        string LoadLegacyApiKey();
        void ClearLegacyApiKey();
        void ClearLegacySettingsPreservingApiKey();
    }
}
