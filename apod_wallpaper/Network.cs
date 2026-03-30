using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Text;

namespace apod_wallpaper
{
    internal static class Network
    {
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);
        private const string UserAgent = "apod_wallpaper/1.0";

        static Network()
        {
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls |
                SecurityProtocolType.Tls11 |
                SecurityProtocolType.Tls12;
        }

        public static string DownloadString(string url)
        {
            return ExecuteWithRetry(
                url,
                () =>
                {
                    using (var client = CreateHttpClient())
                    {
                        return client.GetStringAsync(url).GetAwaiter().GetResult();
                    }
                },
                DownloadStringWithWebRequest);
        }

        public static Bitmap DownloadBitmap(string url)
        {
            return ExecuteWithRetry(
                url,
                () =>
                {
                    using (var client = CreateHttpClient())
                    using (var stream = client.GetStreamAsync(url).GetAwaiter().GetResult())
                    using (var bitmap = new Bitmap(stream))
                    {
                        return new Bitmap(bitmap);
                    }
                },
                DownloadBitmapWithWebRequest);
        }

        private static T ExecuteWithRetry<T>(string url, Func<T> primaryAction, Func<string, T> fallbackAction)
        {
            Exception lastException = null;

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    return primaryAction();
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    AppLogger.Warn("Network primary attempt " + attempt + " failed for " + url + ".", ex);

                    if (attempt < 3)
                        System.Threading.Thread.Sleep(RetryDelay);
                }
            }

            try
            {
                return fallbackAction(url);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Network fallback failed for " + url + ".", ex);
                throw lastException ?? ex;
            }
        }

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                Proxy = WebRequest.DefaultWebProxy,
                UseProxy = true,
                DefaultProxyCredentials = CredentialCache.DefaultCredentials,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                SslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12,
            };

            if (ShouldAllowInvalidCertificates())
            {
                handler.ServerCertificateCustomValidationCallback = (request, certificate, chain, errors) => true;
            }

            var client = new HttpClient(handler)
            {
                Timeout = RequestTimeout,
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
            return client;
        }

        private static bool ShouldAllowInvalidCertificates()
        {
            return string.Equals(
                Environment.GetEnvironmentVariable("APOD_ALLOW_INVALID_CERTIFICATES"),
                "true",
                StringComparison.OrdinalIgnoreCase);
        }

        private static string DownloadStringWithWebRequest(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.UserAgent = UserAgent;
            request.Proxy = WebRequest.DefaultWebProxy;
            request.Proxy.Credentials = CredentialCache.DefaultCredentials;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Timeout = (int)RequestTimeout.TotalMilliseconds;
            request.ReadWriteTimeout = (int)RequestTimeout.TotalMilliseconds;

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream ?? Stream.Null, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static Bitmap DownloadBitmapWithWebRequest(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.UserAgent = UserAgent;
            request.Proxy = WebRequest.DefaultWebProxy;
            request.Proxy.Credentials = CredentialCache.DefaultCredentials;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Timeout = (int)RequestTimeout.TotalMilliseconds;
            request.ReadWriteTimeout = (int)RequestTimeout.TotalMilliseconds;

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var bitmap = new Bitmap(stream))
            {
                return new Bitmap(bitmap);
            }
        }
    }
}
