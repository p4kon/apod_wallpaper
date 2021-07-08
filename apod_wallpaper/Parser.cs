using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;

namespace apod_wallpaper
{
    class Parser
    {
        private string url;
        public static string img_url;
        public static bool isExistUrl = false;
        public static string url_result = "not found";

        public Parser(string url)
        {
            this.url = url;
        }

        public static string ImgUrl
        {
            get
            {
                return img_url;
            }

            set
            {
                img_url = value;
            }
        }

        public static bool IsExistUrl
        {
            get
            {
                return isExistUrl;
            }
            
            set
            {
                isExistUrl = value;
            }
        }

        private static string DeleteTag(string url)
        {
            url = url.Remove(0, 9);
            return url;
        }

        public void GetUrl()
        {
            string data = GetHtmlPageText(url);
            string pattern = string.Format(@"<a\b[^\<\>]+?\bhref\s*=\s*[""'](?<L>.+?)[""'][^\<\>]*?\>");
            Regex regex = new Regex(pattern, RegexOptions.ExplicitCapture);
            if (!(string.IsNullOrEmpty(url)))
            {
                MatchCollection matches = regex.Matches(data);
                foreach (Match match in matches)
                {
                    if (DeleteTag(match.Value).StartsWith("image/"))
                    {
                        img_url = DeleteTag(match.Value);
                        img_url = TodayUrl.GetSiteString() + img_url.SubstringRange(0, img_url.IndexOf('"') - 1);
                        url_result = "ok";
                        isExistUrl = true;
                    }
                }
            }
        }

        public static string GetHtmlPageText(string url)
        {
            WebClient client = new WebClient();
            Network.SetCredentails(client);
            try
            {
                using (Stream data = client.OpenRead(url))
                {
                    using (StreamReader reader = new StreamReader(data))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (WebException we)
            {
                Console.WriteLine(we.Message);
                return null;
            }
        }
    }
}
