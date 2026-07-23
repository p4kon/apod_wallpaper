namespace apod_wallpaper
{
    public sealed class ApplicationSettingsSnapshot
    {
        public const string LanguageEnglish = "en";
        public const string LanguageRussian = "ru";

        public bool TrayDoubleClickAction { get; set; }
        public int WallpaperStyleIndex { get; set; }
        public bool AutoRefreshEnabled { get; set; }
        public string AutoWallpaperSource { get; set; }
        public bool StartWithWindows { get; set; }
        public bool MinimizeToTrayOnClose { get; set; }
        public string Language { get; set; }
        public string NasaApiKey { get; set; }
        public string NasaApiKeyValidationState { get; set; }
        public string ImagesDirectoryPath { get; set; }
        public string TranslationTargetLanguage { get; set; }
        public string LastAutoRefreshRunDate { get; set; }
        public string LastAutoRefreshAppliedDate { get; set; }
        public string LastFavoriteWallpaperDate { get; set; }
        public string LastAppliedWallpaperImagePath { get; set; }
        public bool AutoCheckUpdatesEnabled { get; set; }
        public bool SuppressAutomaticUpdateReminder { get; set; }
        public string LastUpdateCheckUtc { get; set; }

        public ApplicationSettingsSnapshot Clone()
        {
            return new ApplicationSettingsSnapshot
            {
                TrayDoubleClickAction = TrayDoubleClickAction,
                WallpaperStyleIndex = WallpaperStyleIndex,
                AutoRefreshEnabled = AutoRefreshEnabled,
                AutoWallpaperSource = NormalizeAutoWallpaperSource(AutoWallpaperSource),
                StartWithWindows = StartWithWindows,
                MinimizeToTrayOnClose = MinimizeToTrayOnClose,
                Language = NormalizeLanguage(Language),
                NasaApiKey = NasaApiKey,
                NasaApiKeyValidationState = NasaApiKeyValidationState,
                ImagesDirectoryPath = ImagesDirectoryPath,
                TranslationTargetLanguage = NormalizeTranslationTargetLanguage(TranslationTargetLanguage),
                LastAutoRefreshRunDate = LastAutoRefreshRunDate,
                LastAutoRefreshAppliedDate = LastAutoRefreshAppliedDate,
                LastFavoriteWallpaperDate = LastFavoriteWallpaperDate,
                LastAppliedWallpaperImagePath = LastAppliedWallpaperImagePath,
                AutoCheckUpdatesEnabled = AutoCheckUpdatesEnabled,
                SuppressAutomaticUpdateReminder = SuppressAutomaticUpdateReminder,
                LastUpdateCheckUtc = LastUpdateCheckUtc,
            };
        }

        public static string NormalizeLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
                return LanguageEnglish;

            var normalized = language.Trim().ToLowerInvariant();
            if (normalized == "system" || normalized == LanguageEnglish || normalized == "en-us" || normalized == "english")
                return LanguageEnglish;
            if (normalized == LanguageRussian || normalized == "ru-ru" || normalized == "russian")
                return LanguageRussian;

            return LanguageEnglish;
        }

        public static string NormalizeTranslationTargetLanguage(string language)
        {
            return apod_wallpaper.TranslationTargetLanguage.Normalize(language);
        }

        public static string NormalizeAutoWallpaperSource(string source)
        {
            return apod_wallpaper.AutoWallpaperSource.Normalize(source);
        }
    }
}
