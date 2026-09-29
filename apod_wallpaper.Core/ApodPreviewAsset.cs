using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    public static class ApodPreviewAsset
    {
        private const long MaximumBytes = 64L * 1024 * 1024;
        private static readonly Lazy<HttpClient> Client = new Lazy<HttpClient>(() => new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        }) { Timeout = Timeout.InfiniteTimeSpan });

        private sealed class InvalidPreviewException : IOException
        {
            internal InvalidPreviewException() : base("NASA preview is missing or is not a valid image.") { }
        }

        public static Task<string> DownloadAsync(string previewUrl, string sourceUrl, string cachePath, CancellationToken token = default(CancellationToken))
        {
            return DownloadAsync(previewUrl, sourceUrl, cachePath, SendAsync, TimeSpan.FromSeconds(30), token);
        }

        internal static async Task<string> DownloadAsync(string previewUrl, string sourceUrl, string cachePath,
            Func<Uri, CancellationToken, Task<ApodScienceResponse>> send, TimeSpan timeout, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            var preview = new Uri(previewUrl, UriKind.Absolute);
            if (preview.Scheme != Uri.UriSchemeHttps || !preview.IsDefaultPort || !string.IsNullOrEmpty(preview.UserInfo))
                throw new ArgumentException("Preview requires an HTTPS image URL.", nameof(previewUrl));
            if (await Task.Run(() => LocalImageValidator.IsUsableImageFile(cachePath), token).ConfigureAwait(false)) return cachePath;
            var canFallback = !string.IsNullOrWhiteSpace(sourceUrl) && sourceUrl != previewUrl
                && string.Equals(ApodScienceImageUrls.GetPreviewUrl(sourceUrl), previewUrl, StringComparison.Ordinal);
            using (var budget = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                budget.CancelAfter(timeout);
                var temp = cachePath + "." + Guid.NewGuid().ToString("N") + ".download";
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(cachePath)));
                    try { await ReadImageAsync(preview, temp, send, budget.Token).ConfigureAwait(false); }
                    catch (InvalidPreviewException) when (canFallback && !budget.IsCancellationRequested)
                    {
                        await ReadImageAsync(new Uri(sourceUrl), temp, send, budget.Token).ConfigureAwait(false);
                    }
                    budget.Token.ThrowIfCancellationRequested();
                    if (File.Exists(cachePath)) File.Replace(temp, cachePath, null);
                    else File.Move(temp, cachePath);
                    return cachePath;
                }
                catch (Exception ex) when (budget.IsCancellationRequested && (ex is OperationCanceledException || ex is IOException || ex is ObjectDisposedException))
                {
                    token.ThrowIfCancellationRequested();
                    throw new TimeoutException("NASA preview download timed out.", ex);
                }
                finally
                {
                    try { if (File.Exists(temp)) File.Delete(temp); }
                    catch (IOException ex) { AppLogger.Warn("Unable to remove incomplete preview file.", ex); }
                    catch (UnauthorizedAccessException ex) { AppLogger.Warn("Unable to remove incomplete preview file.", ex); }
                }
            }
        }

        private static async Task ReadImageAsync(Uri uri, string path,
            Func<Uri, CancellationToken, Task<ApodScienceResponse>> send, CancellationToken token)
        {
            using (var response = await send(uri, token).ConfigureAwait(false))
            {
                token.ThrowIfCancellationRequested();
                if (response.Status == 404) throw new InvalidPreviewException();
                if (response.Status != 200) throw new IOException("NASA preview HTTP " + response.Status + ".");
                if (response.ContentLength > MaximumBytes) throw new IOException("NASA preview exceeds size limit.");
                if (!(response.ContentType ?? "").StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidPreviewException();
                using (token.Register(response.Dispose))
                using (var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[81920];
                    long total = 0;
                    int read;
                    while ((read = await response.Body.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) != 0)
                    {
                        total += read;
                        if (total > MaximumBytes) throw new IOException("NASA preview exceeds size limit.");
                        await file.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                    }
                }
            }
            var valid = await Task.Run(() =>
            {
                try
                {
                    using (var stream = File.OpenRead(path))
                    using (var image = Image.FromStream(stream, false, true))
                        return image.Width > 0 && image.Height > 0 && (long)image.Width * image.Height <= 100000000;
                }
                catch (ArgumentException) { return false; }
                catch (OutOfMemoryException) { return false; }
            }, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (!valid) throw new InvalidPreviewException();
        }

        private static async Task<ApodScienceResponse> SendAsync(Uri uri, CancellationToken token)
        {
            var response = await Client.Value.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            try
            {
                var body = response.StatusCode == HttpStatusCode.OK ? await response.Content.ReadAsStreamAsync().ConfigureAwait(false) : Stream.Null;
                return new ApodScienceResponse((int)response.StatusCode, body, response.Content.Headers.ContentType?.MediaType,
                    response.Content.Headers.ContentLength, dispose: response.Dispose);
            }
            catch { response.Dispose(); throw; }
        }
    }
}
