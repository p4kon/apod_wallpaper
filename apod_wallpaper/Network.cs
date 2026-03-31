using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
                "text",
                url,
                () =>
                {
                    using (var client = CreateHttpClient())
                    {
                        return DownloadStringWithHttpClient(client, url);
                    }
                },
                DownloadStringWithWebRequest);
        }

        public static Task<string> DownloadStringAsync(string url)
        {
            return ExecuteWithRetryAsync(
                "text",
                url,
                async () =>
                {
                    using (var client = CreateHttpClient())
                    {
                        return await DownloadStringWithHttpClientAsync(client, url).ConfigureAwait(false);
                    }
                },
                async requestUrl => await Task.Run(() => DownloadStringWithWebRequest(requestUrl)).ConfigureAwait(false));
        }

        public static Bitmap DownloadBitmap(string url)
        {
            return ExecuteWithRetry(
                "bitmap",
                url,
                () =>
                {
                    using (var client = CreateHttpClient())
                    using (var response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
                    using (var stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                    using (var bitmap = new Bitmap(stream))
                    {
                        response.EnsureSuccessStatusCode();
                        AppLogger.Web("transport=httpclient kind=bitmap stage=success status=" + (int)response.StatusCode + " url=" + url);
                        return new Bitmap(bitmap);
                    }
                },
                DownloadBitmapWithWebRequest);
        }

        public static Task<Bitmap> DownloadBitmapAsync(string url)
        {
            return ExecuteWithRetryAsync(
                "bitmap",
                url,
                async () =>
                {
                    using (var client = CreateHttpClient())
                    using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var bitmap = new Bitmap(stream))
                    {
                        response.EnsureSuccessStatusCode();
                        AppLogger.Web("transport=httpclient kind=bitmap stage=success status=" + (int)response.StatusCode + " url=" + url);
                        return new Bitmap(bitmap);
                    }
                },
                async requestUrl => await Task.Run(() => DownloadBitmapWithWebRequest(requestUrl)).ConfigureAwait(false));
        }

        private static T ExecuteWithRetry<T>(string kind, string url, Func<T> primaryAction, Func<string, T> fallbackAction)
        {
            Exception lastException = null;

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    AppLogger.Web("transport=httpclient kind=" + kind + " stage=start attempt=" + attempt + " url=" + url);
                    return primaryAction();
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    AppLogger.Warn("Network primary attempt " + attempt + " failed for kind=" + kind + " url=" + url + ".", ex);

                    if (attempt < 3)
                        System.Threading.Thread.Sleep(RetryDelay);
                }
            }

            try
            {
                AppLogger.Web("transport=webrequest kind=" + kind + " stage=start url=" + url);
                return fallbackAction(url);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Network fallback failed for kind=" + kind + " url=" + url + ".", ex);
                throw lastException ?? ex;
            }
        }

        private static async Task<T> ExecuteWithRetryAsync<T>(string kind, string url, Func<Task<T>> primaryAction, Func<string, Task<T>> fallbackAction)
        {
            Exception lastException = null;

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    AppLogger.Web("transport=httpclient kind=" + kind + " stage=start attempt=" + attempt + " url=" + url);
                    return await primaryAction().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    AppLogger.Warn("Network primary attempt " + attempt + " failed for kind=" + kind + " url=" + url + ".", ex);

                    if (attempt < 3)
                        await Task.Delay(RetryDelay).ConfigureAwait(false);
                }
            }

            try
            {
                AppLogger.Web("transport=webrequest kind=" + kind + " stage=start url=" + url);
                return await fallbackAction(url).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Network fallback failed for kind=" + kind + " url=" + url + ".", ex);
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

        private static string DownloadStringWithHttpClient(HttpClient client, string url)
        {
            using (var response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
            {
                var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                AppLogger.Web("transport=httpclient kind=text stage=response status=" + (int)response.StatusCode + " url=" + url);
                response.EnsureSuccessStatusCode();
                return content;
            }
        }

        private static async Task<string> DownloadStringWithHttpClientAsync(HttpClient client, string url)
        {
            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AppLogger.Web("transport=httpclient kind=text stage=response status=" + (int)response.StatusCode + " url=" + url);
                response.EnsureSuccessStatusCode();
                return content;
            }
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

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream ?? Stream.Null, Encoding.UTF8))
                {
                    AppLogger.Web("transport=webrequest kind=text stage=success status=" + (int)response.StatusCode + " url=" + url);
                    return reader.ReadToEnd();
                }
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse response)
            {
                AppLogger.Warn("HttpWebRequest fallback returned HTTP " + (int)response.StatusCode + " for text url=" + url + ".", ex);
                return null;
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

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var bitmap = new Bitmap(stream))
                {
                    AppLogger.Web("transport=webrequest kind=bitmap stage=success status=" + (int)response.StatusCode + " url=" + url);
                    return new Bitmap(bitmap);
                }
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse response)
            {
                AppLogger.Warn("HttpWebRequest fallback returned HTTP " + (int)response.StatusCode + " for bitmap url=" + url + ".", ex);
                return null;
            }
        }
    }
}
