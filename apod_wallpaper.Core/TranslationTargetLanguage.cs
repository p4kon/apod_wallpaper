using System;
using System.Collections.Generic;
using System.Text;

namespace apod_wallpaper
{
    public static class TranslationTargetLanguage
    {
        public const string Russian = "ru";
        public const string Spanish = "es";
        public const string German = "de";
        public const string French = "fr";
        public const string Italian = "it";
        public const string Portuguese = "pt";
        public const string Japanese = "ja";

        private static readonly HashSet<string> SupportedLanguages = new HashSet<string>(StringComparer.Ordinal)
        {
            Russian,
            Spanish,
            German,
            French,
            Italian,
            Portuguese,
            Japanese,
        };

        public static string Normalize(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
                return Russian;

            var normalized = language.Trim().ToLowerInvariant();
            return SupportedLanguages.Contains(normalized) ? normalized : Russian;
        }

        public static bool IsSupported(string language)
        {
            return !string.IsNullOrWhiteSpace(language) && SupportedLanguages.Contains(language.Trim().ToLowerInvariant());
        }

        public static string GetDisplayCode(string language)
        {
            return Normalize(language);
        }

        public static string BuildGoogleTranslateUrl(string targetLanguage, string explanationText, bool includeText)
        {
            var normalizedTargetLanguage = Normalize(targetLanguage);
            if (string.IsNullOrEmpty(normalizedTargetLanguage))
                throw new ArgumentException("A supported target language is required.", nameof(targetLanguage));

            var builder = new StringBuilder("https://translate.google.com/?sl=en&tl=");
            builder.Append(normalizedTargetLanguage);

            if (includeText)
            {
                builder.Append("&text=");
                builder.Append(EscapeDataString(explanationText ?? string.Empty));
            }

            builder.Append("&op=translate");
            return builder.ToString();
        }

        private static string EscapeDataString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            const int MaxChunkLength = 30000;
            if (value.Length <= MaxChunkLength)
                return Uri.EscapeDataString(value);

            var builder = new StringBuilder(value.Length);
            for (var index = 0; index < value.Length; index += MaxChunkLength)
            {
                var length = Math.Min(MaxChunkLength, value.Length - index);
                builder.Append(Uri.EscapeDataString(value.Substring(index, length)));
            }

            return builder.ToString();
        }
    }
}
