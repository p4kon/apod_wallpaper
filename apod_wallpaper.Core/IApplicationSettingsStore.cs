namespace apod_wallpaper
{
    public interface IApplicationSettingsStore
    {
        bool Exists();
        ApplicationSettingsSnapshot Load();
        void Save(ApplicationSettingsSnapshot settings);
    }
}
