using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    internal sealed class ApodOriginalAsset
    {
        private const long MaximumBytes = 256L * 1024 * 1024;
        private static readonly Lazy<HttpClient> Client = new Lazy<HttpClient>(() => new HttpClient(new HttpClientHandler
        { AllowAutoRedirect = false, UseCookies = false }) { Timeout = Timeout.InfiniteTimeSpan });
        private readonly Func<Uri, CancellationToken, ApodScienceResponse> _send;
        private readonly Func<Uri, CancellationToken, Task<ApodScienceResponse>> _sendAsync;
        private readonly TimeSpan _timeout;

        internal ApodOriginalAsset(Func<Uri, CancellationToken, ApodScienceResponse> send = null,
            Func<Uri, CancellationToken, Task<ApodScienceResponse>> sendAsync = null, TimeSpan? timeout = null)
        {
            _send = send ?? Send;
            _sendAsync = sendAsync ?? SendAsync;
            _timeout = timeout ?? TimeSpan.FromMinutes(2);
        }

        internal static bool Handles(ApodEntry entry)
        {
            return entry != null && !string.IsNullOrWhiteSpace(entry.SourceOriginalUrl)
                && !string.IsNullOrWhiteSpace(entry.BestImageUrl)
                && entry.BestImageUrl.StartsWith("https://assets.science.nasa.gov/content/dam/science/", StringComparison.Ordinal)
                && string.Equals(ApodScienceImageUrls.GetOriginalUrl(entry.SourceOriginalUrl), entry.BestImageUrl, StringComparison.Ordinal);
        }

        internal void Download(string url, string path)
        {
            var temp = Prepare(url, path);
            using (var budget = new CancellationTokenSource(_timeout))
            {
                try
                {
                    using (var response = _send(new Uri(url), budget.Token))
                    {
                        ValidateResponse(response);
                        using (budget.Token.Register(response.Dispose))
                        using (var file = File.Create(temp))
                        {
                            var buffer = new byte[81920];
                            long total = 0;
                            int count;
                            while ((count = response.Body.Read(buffer, 0, buffer.Length)) != 0)
                            {
                                budget.Token.ThrowIfCancellationRequested();
                                total += count;
                                CheckSize(total);
                                file.Write(buffer, 0, count);
                            }
                        }
                    }
                    ValidateImage(temp);
                    budget.Token.ThrowIfCancellationRequested();
                    Publish(temp, path);
                }
                catch (Exception ex) when (budget.IsCancellationRequested && IsCancellationFailure(ex))
                { throw new TimeoutException("NASA original download timed out.", ex); }
                finally { Cleanup(temp); }
            }
        }

        internal async Task DownloadAsync(string url, string path, IProgress<DownloadProgressSnapshot> progress = null)
        {
            var temp = Prepare(url, path);
            using (var budget = new CancellationTokenSource(_timeout))
            {
                try
                {
                    using (var response = await _sendAsync(new Uri(url), budget.Token).ConfigureAwait(false))
                    {
                        ValidateResponse(response);
                        using (budget.Token.Register(response.Dispose))
                        using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                        {
                            var watch = Stopwatch.StartNew();
                            var buffer = new byte[81920];
                            long total = 0;
                            progress?.Report(new DownloadProgressSnapshot { BytesReceived = 0, TotalBytes = response.ContentLength });
                            int count;
                            while ((count = await response.Body.ReadAsync(buffer, 0, buffer.Length, budget.Token).ConfigureAwait(false)) != 0)
                            {
                                total += count;
                                CheckSize(total);
                                await file.WriteAsync(buffer, 0, count, budget.Token).ConfigureAwait(false);
                                progress?.Report(new DownloadProgressSnapshot { BytesReceived = total, TotalBytes = response.ContentLength,
                                    BytesPerSecond = total / Math.Max(watch.Elapsed.TotalSeconds, 0.001) });
                            }
                        }
                    }
                    await Task.Run(() => ValidateImage(temp), budget.Token).ConfigureAwait(false);
                    budget.Token.ThrowIfCancellationRequested();
                    Publish(temp, path);
                }
                catch (Exception ex) when (budget.IsCancellationRequested && IsCancellationFailure(ex))
                { throw new TimeoutException("NASA original download timed out.", ex); }
                finally { Cleanup(temp); }
            }
        }

        private static string Prepare(string url, string path)
        {
            var uri = new Uri(url, UriKind.Absolute);
            if (uri.Scheme != "https" || uri.Host != "assets.science.nasa.gov" || !uri.IsDefaultPort
                || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
                || !uri.AbsolutePath.StartsWith("/content/dam/science/", StringComparison.Ordinal))
                throw new ArgumentException("Original requires a static NASA asset URL.", nameof(url));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            return path + "." + Guid.NewGuid().ToString("N") + ".download";
        }

        private static void ValidateResponse(ApodScienceResponse response)
        {
            if (response.Status != 200) throw new IOException("NASA original HTTP " + response.Status + ".");
            if (!(response.ContentType ?? "").StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("NASA original response is not an image.");
            CheckSize(response.ContentLength ?? 0);
        }

        private static void CheckSize(long size)
        { if (size > MaximumBytes) throw new IOException("NASA original exceeds size limit."); }

        private static void ValidateImage(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                using (var image = Image.FromStream(stream, false, true))
                    if (image.Width <= 0 || image.Height <= 0) throw new InvalidDataException("Empty NASA original image.");
            }
            catch (ArgumentException ex) { throw new InvalidDataException("Invalid NASA original image.", ex); }
            catch (OutOfMemoryException ex) { throw new InvalidDataException("Invalid or oversized NASA original image.", ex); }
        }

        private static void Publish(string temp, string path)
        { if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path); }

        private static void Cleanup(string temp)
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch (IOException ex) { AppLogger.Warn("Unable to remove incomplete original.", ex); }
            catch (UnauthorizedAccessException ex) { AppLogger.Warn("Unable to remove incomplete original.", ex); }
        }

        private static bool IsCancellationFailure(Exception ex)
        { return ex is OperationCanceledException || ex is IOException || ex is ObjectDisposedException || ex is WebException; }

        private static async Task<ApodScienceResponse> SendAsync(Uri uri, CancellationToken token)
        {
            var response = await Client.Value.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            try
            {
                var stream = response.StatusCode == HttpStatusCode.OK ? await response.Content.ReadAsStreamAsync().ConfigureAwait(false) : Stream.Null;
                return new ApodScienceResponse((int)response.StatusCode, stream, response.Content.Headers.ContentType?.MediaType,
                    response.Content.Headers.ContentLength, dispose: response.Dispose);
            }
            catch { response.Dispose(); throw; }
        }

        private static ApodScienceResponse Send(Uri uri, CancellationToken token)
        {
#if NET48
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.AllowAutoRedirect = false;
            request.Timeout = 120000;
            request.ReadWriteTimeout = 120000;
            var abort = token.Register(request.Abort);
            HttpWebResponse response = null;
            try
            {
                try { response = (HttpWebResponse)request.GetResponse(); }
                catch (WebException ex) when (ex.Response is HttpWebResponse) { response = (HttpWebResponse)ex.Response; }
                return new ApodScienceResponse((int)response.StatusCode, response.GetResponseStream(), response.ContentType.Split(';')[0],
                    response.ContentLength < 0 ? (long?)null : response.ContentLength, dispose: () => { abort.Dispose(); response.Dispose(); });
            }
            catch { abort.Dispose(); response?.Dispose(); throw; }
#else
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                var response = Client.Value.Send(request, HttpCompletionOption.ResponseHeadersRead, token);
                try
                {
                    return new ApodScienceResponse((int)response.StatusCode,
                        response.StatusCode == HttpStatusCode.OK ? response.Content.ReadAsStream(token) : Stream.Null,
                        response.Content.Headers.ContentType?.MediaType, response.Content.Headers.ContentLength, dispose: response.Dispose);
                }
                catch { response.Dispose(); throw; }
            }
#endif
        }
    }
}
