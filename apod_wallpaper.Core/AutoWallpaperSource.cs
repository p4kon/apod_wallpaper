namespace apod_wallpaper
{
    public static class AutoWallpaperSource
    {
        public const string Latest = "latest";
        public const string Favorites = "favorites";

        public static string Normalize(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return Latest;

            var normalized = source.Trim().ToLowerInvariant();
            if (normalized == Favorites || normalized == "favorite")
                return Favorites;

            return Latest;
        }
    }
}
