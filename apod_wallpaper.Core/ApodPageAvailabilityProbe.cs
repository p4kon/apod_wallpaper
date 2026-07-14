using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    internal sealed class ApodPageAvailabilityProbe
    {
        private static readonly Regex ApodPageRegex = new Regex(@"/apod/ap(?<date>\d{6})\.html$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Lazy<HttpClient> SharedHttpClient = new Lazy<HttpClient>(CreateHttpClient);

        public async Task<ApodPageAvailabilityProbeResult> ProbeAsync(DateTime date, TimeSpan timeout)
        {
            var expectedUrl = ApodPageUrl.BuildUrl(date);
            using (var cancellation = new CancellationTokenSource(timeout))
            {
                var headResult = await ProbeWithMethodAsync(date, expectedUrl, HttpMethod.Head, cancellation.Token).ConfigureAwait(false);
                if (!ShouldRetryWithGet(headResult))
                    return headResult;

                return await ProbeWithMethodAsync(date, expectedUrl, HttpMethod.Get, cancellation.Token).ConfigureAwait(false);
            }
        }

        internal static ApodPageAvailabilityProbeResult EvaluateResponse(
            DateTime date,
            string expectedUrl,
            HttpStatusCode statusCode,
            string method,
            Uri redirectLocation,
            Uri effectiveUri)
        {
            if (IsRedirectStatus(statusCode))
            {
                var resolvedRedirect = ResolveRedirectUri(expectedUrl, redirectLocation);
                return ApodPageAvailabilityProbeResult.Unavailable(
                    date,
                    expectedUrl,
                    statusCode,
                    method,
                    resolvedRedirect != null ? resolvedRedirect.ToString() : null);
            }

            if ((int)statusCode >= 200 && (int)statusCode <= 299)
            {
                var resolvedUri = effectiveUri ?? new Uri(expectedUrl, UriKind.Absolute);
                if (IsSameApodPageDate(date, resolvedUri))
                    return ApodPageAvailabilityProbeResult.Available(date, expectedUrl, statusCode, method);

                return ApodPageAvailabilityProbeResult.Unavailable(date, expectedUrl, statusCode, method, resolvedUri.ToString());
            }

            if (statusCode == HttpStatusCode.NotFound)
                return ApodPageAvailabilityProbeResult.Unavailable(date, expectedUrl, statusCode, method);

            return ApodPageAvailabilityProbeResult.Unknown(date, expectedUrl, method, statusCode);
        }

        private static async Task<ApodPageAvailabilityProbeResult> ProbeWithMethodAsync(DateTime date, string expectedUrl, HttpMethod method, CancellationToken cancellationToken)
        {
            try
            {
                using (var request = new HttpRequestMessage(method, expectedUrl))
                using (var response = await SharedHttpClient.Value.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    return EvaluateResponse(
                        date,
                        expectedUrl,
                        response.StatusCode,
                        method.Method,
                        response.Headers.Location,
                        response.RequestMessage != null ? response.RequestMessage.RequestUri : null);
                }
            }
            catch (OperationCanceledException ex)
            {
                return ApodPageAvailabilityProbeResult.Unknown(date, expectedUrl, method.Method, errorMessage: ex.Message);
            }
            catch (HttpRequestException ex)
            {
                return ApodPageAvailabilityProbeResult.Unknown(date, expectedUrl, method.Method, errorMessage: ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ApodPageAvailabilityProbeResult.Unknown(date, expectedUrl, method.Method, errorMessage: ex.Message);
            }
        }

        internal static bool ShouldRetryWithGet(ApodPageAvailabilityProbeResult result)
        {
            return result != null &&
                string.Equals(result.Method, HttpMethod.Head.Method, StringComparison.OrdinalIgnoreCase) &&
                result.StatusCode.HasValue &&
                (result.StatusCode.Value == HttpStatusCode.Forbidden ||
                 result.StatusCode.Value == HttpStatusCode.MethodNotAllowed ||
                 (int)result.StatusCode.Value == 501);
        }

        private static bool IsRedirectStatus(HttpStatusCode statusCode)
        {
            var code = (int)statusCode;
            return code >= 300 && code <= 399;
        }

        private static Uri ResolveRedirectUri(string expectedUrl, Uri redirectLocation)
        {
            if (redirectLocation == null)
                return null;

            if (redirectLocation.IsAbsoluteUri)
                return redirectLocation;

            return new Uri(new Uri(expectedUrl, UriKind.Absolute), redirectLocation);
        }

        private static bool IsSameApodPageDate(DateTime date, Uri uri)
        {
            if (uri == null)
                return false;

            var match = ApodPageRegex.Match(uri.AbsolutePath);
            if (!match.Success)
                return false;

            var expectedCode = date.Date.ToString("yyMMdd", CultureInfo.InvariantCulture);
            return string.Equals(match.Groups["date"].Value, expectedCode, StringComparison.Ordinal);
        }

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                SslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12,
            };

            return new HttpClient(handler)
            {
                Timeout = Timeout.InfiniteTimeSpan,
            };
        }
    }
}
