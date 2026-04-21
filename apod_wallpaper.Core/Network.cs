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
        private static readonly Lazy<HttpClient> SharedHttpClient = new Lazy<HttpClient>(CreateHttpClient, true);

        static Network()
        {
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls |
                SecurityProtocolType.Tls11 |
                SecurityProtocolType.Tls12;
        }

        public static string DownloadString(string url)
        {
            return DownloadString(url, NetworkRetryProfile.Default);
        }

        public static string DownloadString(string url, NetworkRetryProfile retryProfile)
        {
            return ExecuteWithRetry(
                "text",
                url,
                retryProfile,
                () => DownloadStringWithHttpClient(SharedHttpClient.Value, url),
                DownloadStringWithWebRequest);
        }

        public static Task<string> DownloadStringAsync(string url)
        {
            return DownloadStringAsync(url, NetworkRetryProfile.Default);
        }

        public static Task<string> DownloadStringAsync(string url, NetworkRetryProfile retryProfile)
        {
            return ExecuteWithRetryAsync(
                "text",
                url,
                retryProfile,
                async () => await DownloadStringWithHttpClientAsync(SharedHttpClient.Value, url).ConfigureAwait(false),
                async requestUrl => await Task.Run(() => DownloadStringWithWebRequest(requestUrl)).ConfigureAwait(false));
        }

        public static Bitmap DownloadBitmap(string url)
        {
            return ExecuteWithRetry(
                "bitmap",
                url,
                NetworkRetryProfile.Default,
                () => DownloadBitmapWithHttpClient(SharedHttpClient.Value, url),
                DownloadBitmapWithWebRequest);
        }

        public static Task<Bitmap> DownloadBitmapAsync(string url)
        {
            return ExecuteWithRetryAsync(
                "bitmap",
                url,
                NetworkRetryProfile.Default,
                async () => await DownloadBitmapWithHttpClientAsync(SharedHttpClient.Value, url).ConfigureAwait(false),
                async requestUrl => await Task.Run(() => DownloadBitmapWithWebRequest(requestUrl)).ConfigureAwait(false));
        }

        private static T ExecuteWithRetry<T>(string kind, string url, NetworkRetryProfile retryProfile, Func<T> primaryAction, Func<string, T> fallbackAction)
        {
            Exception lastException = null;
            var maxAttempts = ResolvePrimaryAttempts(kind, url, retryProfile);

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
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

                    if (!ShouldRetry(ex, attempt, maxAttempts))
                        break;

                    if (attempt < maxAttempts)
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

        private static async Task<T> ExecuteWithRetryAsync<T>(string kind, string url, NetworkRetryProfile retryProfile, Func<Task<T>> primaryAction, Func<string, Task<T>> fallbackAction)
        {
            Exception lastException = null;
            var maxAttempts = ResolvePrimaryAttempts(kind, url, retryProfile);

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
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

                    if (!ShouldRetry(ex, attempt, maxAttempts))
                        break;

                    if (attempt < maxAttempts)
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
                EnsureSuccessfulStatusCode(response.StatusCode, url);
                return NormalizeTextContent(content);
            }
        }

        private static async Task<string> DownloadStringWithHttpClientAsync(HttpClient client, string url)
        {
            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AppLogger.Web("transport=httpclient kind=text stage=response status=" + (int)response.StatusCode + " url=" + url);
                EnsureSuccessfulStatusCode(response.StatusCode, url);
                return NormalizeTextContent(content);
            }
        }

        private static Bitmap DownloadBitmapWithHttpClient(HttpClient client, string url)
        {
            using (var response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
            {
                AppLogger.Web("transport=httpclient kind=bitmap stage=response status=" + (int)response.StatusCode + " url=" + url);
                EnsureSuccessfulStatusCode(response.StatusCode, url);

                using (var stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                using (var bitmap = new Bitmap(stream))
                {
                    return new Bitmap(bitmap);
                }
            }
        }

        private static async Task<Bitmap> DownloadBitmapWithHttpClientAsync(HttpClient client, string url)
        {
            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                AppLogger.Web("transport=httpclient kind=bitmap stage=response status=" + (int)response.StatusCode + " url=" + url);
                EnsureSuccessfulStatusCode(response.StatusCode, url);

                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var bitmap = new Bitmap(stream))
                {
                    return new Bitmap(bitmap);
                }
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
                    return NormalizeTextContent(reader.ReadToEnd());
                }
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse response)
            {
                AppLogger.Warn("HttpWebRequest fallback returned HTTP " + (int)response.StatusCode + " for text url=" + url + ".", ex);
                return null;
            }
        }

        private static string NormalizeTextContent(string content)
        {
            if (string.IsNullOrEmpty(content))
                return content;

            var normalized = content.Replace("\0", string.Empty);
            if (!ReferenceEquals(normalized, content) && normalized.Length > 0)
            {
                AppLogger.Web("transport=text stage=normalize removed_nulls=true originalLength=" + content.Length + " normalizedLength=" + normalized.Length);
            }

            return normalized.TrimStart('\uFEFF', '\uFFFE');
        }

        private static void EnsureSuccessfulStatusCode(HttpStatusCode statusCode, string url)
        {
            var numericStatusCode = (int)statusCode;
            if (numericStatusCode >= 200 && numericStatusCode <= 299)
                return;

            throw new NetworkHttpStatusException(numericStatusCode, url);
        }

        private static int ResolvePrimaryAttempts(string kind, string url, NetworkRetryProfile retryProfile)
        {
            if (!string.Equals(kind, "text", StringComparison.OrdinalIgnoreCase))
                return 3;

            if (!IsNasaApiRequest(url))
                return 3;

            switch (retryProfile)
            {
                case NetworkRetryProfile.NasaApiDemo:
                    return 1;
                case NetworkRetryProfile.NasaApiAuthenticated:
                    return 3;
                default:
                    return 3;
            }
        }

        private static bool IsNasaApiRequest(string url)
        {
            return !string.IsNullOrWhiteSpace(url) &&
                   url.IndexOf("api.nasa.gov/planetary/apod", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ShouldRetry(Exception exception, int attempt, int maxAttempts)
        {
            if (attempt >= maxAttempts)
                return false;

            var statusException = exception as NetworkHttpStatusException;
            if (statusException != null)
            {
                switch (statusException.StatusCode)
                {
                    case 408:
                    case 425:
                    case 429:
                    case 500:
                    case 502:
                    case 503:
                    case 504:
                        return true;
                    default:
                        return false;
                }
            }

            return exception is HttpRequestException ||
                   exception is WebException ||
                   exception is IOException ||
                   exception is TaskCanceledException;
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

        [Serializable]
        private sealed class NetworkHttpStatusException : Exception
        {
            public NetworkHttpStatusException(int statusCode, string url)
                : base("HTTP " + statusCode + " for " + url + ".")
            {
                StatusCode = statusCode;
                Url = url;
            }

            public int StatusCode { get; }
            public string Url { get; }
        }
    }
}
