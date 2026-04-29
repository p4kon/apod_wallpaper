namespace apod_wallpaper
{
    internal static class RuntimeSettingsSync
    {
        public static void ApplyCurrentSettings()
        {
            AppRuntimeSettings.Configure(
                UserSecretStore.GetNasaApiKey(),
                Properties.Settings.Default.ImagesDirectoryPath,
                ParseValidationState(Properties.Settings.Default.NasaApiKeyValidationState));
        }

        private static ApiKeyValidationState ParseValidationState(string value)
        {
            ApiKeyValidationState parsedState;
            return System.Enum.TryParse(value, true, out parsedState)
                ? parsedState
                : ApiKeyValidationState.Unknown;
        }
    }
}
