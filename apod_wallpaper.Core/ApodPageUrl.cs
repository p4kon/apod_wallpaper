using System;
using System.Globalization;

namespace apod_wallpaper
{
    internal static class ApodPageUrl
    {
        private static readonly CultureInfo EnglishCulture = new CultureInfo("en-US", false);
        private const string ApodBaseUrl = "https://apod.nasa.gov/apod/";
        private const string ApodFilePrefix = "ap";
        private const string HtmlExtension = ".html";
        private static DateTime _currentDate;
        private static string _currentDateCode;

        public static void SetDate(DateTime date)
        {
            _currentDate = date.Date;
            _currentDateCode = _currentDate.ToString("yyMMdd", EnglishCulture);
        }

        public static string GetUrl(DateTime date)
        {
            SetDate(date);
            return GetUrl();
        }

        public static string GetUrl()
        {
            return ApodBaseUrl + ApodFilePrefix + _currentDateCode + HtmlExtension;
        }

        public static string GetName(DateTime date)
        {
            return GetBaseName(date) + DownloadedImageFile.JpgExtension;
        }

        public static string GetBaseName(DateTime date)
        {
            return date.Date.ToString("yyyy-MM-dd", EnglishCulture);
        }

        public static string GetName()
        {
            return GetName(_currentDate);
        }
    }

}
