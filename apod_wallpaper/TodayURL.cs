using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;

namespace apod_wallpaper
{
    public class TodayUrl
    {
        private static CultureInfo ci = new CultureInfo("en-US", false);
        private static DateTime dt = new DateTime(2021, 6, 25);
        private static DateTime utc_time;
        private readonly static string site_str = "https://apod.nasa.gov/apod/";
        private readonly static string ap = "ap";
        private readonly static string html = ".html";
        private static string date_str;
        private static string name_by_date;
        private static string url;

        public static void SetDate(DateTime date)
        {
            utc_time = (DateTime)date;
            date_str = utc_time.ToString("yyMMdd", ci);
        }

        public static string GetUrl()
        {
            url = site_str + ap + date_str + html;
            return url;
        }
        public static string GetName()
        {
            name_by_date = utc_time.ToString("dd_MMMM_yyyy", ci) + Image.jpg_format;
            return name_by_date;
        }

        public static string GetSiteString()
        {
            return site_str;
        }

        public static DateTime GetUtcTime()
        {
            return utc_time;
        }

    }
}