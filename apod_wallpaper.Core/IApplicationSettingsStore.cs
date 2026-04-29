namespace apod_wallpaper
{
    public interface IApplicationSettingsStore
    {
        ApplicationSettingsSnapshot Load();
        void Save(ApplicationSettingsSnapshot settings);
        string LoadLegacyApiKey();
        void ClearLegacyApiKey();
    }
}
