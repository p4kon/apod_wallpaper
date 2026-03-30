using System;
using System.Globalization;

namespace apod_wallpaper
{
    public class TodayUrl
    {
        private static readonly CultureInfo ci = new CultureInfo("en-US", false);
        private static readonly string site_str = "https://apod.nasa.gov/apod/";
        private static readonly string ap = "ap";
        private static readonly string html = ".html";
        private static DateTime utc_time;
        private static string date_str;

        public static void SetDate(DateTime date)
        {
            utc_time = date.Date;
            date_str = utc_time.ToString("yyMMdd", ci);
        }

        public static string GetUrl(DateTime date)
        {
            SetDate(date);
            return GetUrl();
        }

        public static string GetUrl()
        {
            return site_str + ap + date_str + html;
        }

        public static string GetName(DateTime date)
        {
            return GetBaseName(date) + Image.jpg_format;
        }

        public static string GetBaseName(DateTime date)
        {
            return date.Date.ToString("dd_MMMM_yyyy", ci);
        }

        public static string GetName()
        {
            return GetName(utc_time);
        }
    }
}
