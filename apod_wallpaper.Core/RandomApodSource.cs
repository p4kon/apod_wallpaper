using System;

namespace apod_wallpaper
{
    public static class RandomApodSource
    {
        public const string Global = "global";
        public const string Downloaded = "downloaded";
        public const string Favorites = "favorites";

        public static string Normalize(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return Global;

            var normalized = source.Trim().ToLowerInvariant();
            if (normalized == Downloaded || normalized == "local" || normalized == "library")
                return Downloaded;
            if (normalized == Favorites || normalized == "favorite")
                return Favorites;

            return Global;
        }
    }
}
