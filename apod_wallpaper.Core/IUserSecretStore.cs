namespace apod_wallpaper
{
    public interface IUserSecretStore
    {
        string GetNasaApiKey();
        void SaveNasaApiKey(string apiKey);
        void DeleteNasaApiKey();
    }
}
