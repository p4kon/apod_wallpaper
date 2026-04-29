namespace apod_wallpaper
{
    internal static class AppRuntimeSettings
    {
        private const string DemoApiKey = "DEMO_KEY";

        public static string RawNasaApiKey { get; private set; }
        public static string NasaApiKey { get; private set; }
        public static ApiKeyValidationState NasaApiKeyValidationState { get; private set; }
        public static string ImagesDirectoryPath { get; private set; }

        public static void Configure(string nasaApiKey, string imagesDirectoryPath, ApiKeyValidationState apiKeyValidationState)
        {
            RawNasaApiKey = Normalize(nasaApiKey);
            NasaApiKeyValidationState = apiKeyValidationState;
            NasaApiKey = ResolveEffectiveApiKey(RawNasaApiKey, apiKeyValidationState);
            ImagesDirectoryPath = Normalize(imagesDirectoryPath);
        }

        private static string ResolveEffectiveApiKey(string nasaApiKey, ApiKeyValidationState validationState)
        {
            if (string.IsNullOrWhiteSpace(nasaApiKey))
                return null;

            if (validationState == ApiKeyValidationState.Invalid &&
                !string.Equals(nasaApiKey, DemoApiKey, System.StringComparison.OrdinalIgnoreCase))
            {
                return DemoApiKey;
            }

            return nasaApiKey;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
