using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    internal sealed class ApodClient : IApodClient
    {
        private const string DemoApiKey = "DEMO_KEY";
        private const string Endpoint = "https://api.nasa.gov/planetary/apod";
        private static readonly TimeSpan LatestFallbackLookback = TimeSpan.FromDays(3);
        private static readonly TimeSpan DemoDatedApiBlockDuration = TimeSpan.FromMinutes(15);
        private const int DemoDatedApi403Threshold = 2;
        private static readonly object DemoDatedApiSyncRoot = new object();
        private static int _demoDatedApi403Count;
        private static DateTime _demoDatedApiBlockedUntilUtc;

        public ApodEntry GetEntry(DateTime date)
        {
            var requestUrl = BuildSingleRequestUrl(date);
            if (ShouldShortCircuitDemoDatedApi(requestUrl))
            {
                AppLogger.Web("source=nasa_api scope=single result=short_circuit_demo date=" + date.ToString("yyyy-MM-dd"));
                return CreateEntryFromApodPage(date);
            }

            var retryProfile = ResolveApiRetryProfile();
            AppLogger.Web("source=nasa_api scope=single date=" + date.ToString("yyyy-MM-dd") + " url=" + requestUrl);
            try
            {
                var entry = Deserialize<ApodEntry>(Network.DownloadString(requestUrl, retryProfile), "NASA APOD API response could not be parsed.");
                entry.ResolvedFromSource = "api";
                RecordApiSuccess(requestUrl);
                return NormalizeEntry(entry, date);
            }
            catch (Exception ex)
            {
                RecordApiFailure(requestUrl, ex);
                AppLogger.Warn("NASA APOD API single request failed for " + date.ToString("yyyy-MM-dd") + ".", ex);
                return CreateEntryFromApodPage(date);
            }
        }

        public async Task<ApodEntry> GetEntryAsync(DateTime date)
        {
            var requestUrl = BuildSingleRequestUrl(date);
            if (ShouldShortCircuitDemoDatedApi(requestUrl))
            {
                AppLogger.Web("source=nasa_api scope=single_async result=short_circuit_demo date=" + date.ToString("yyyy-MM-dd"));
                return await CreateEntryFromApodPageAsync(date).ConfigureAwait(false);
            }

            var retryProfile = ResolveApiRetryProfile();
            AppLogger.Web("source=nasa_api scope=single_async date=" + date.ToString("yyyy-MM-dd") + " url=" + requestUrl);
            try
            {
                var entry = Deserialize<ApodEntry>(await Network.DownloadStringAsync(requestUrl, retryProfile).ConfigureAwait(false), "NASA APOD API response could not be parsed.");
                entry.ResolvedFromSource = "api";
                RecordApiSuccess(requestUrl);
                return await NormalizeEntryAsync(entry, date).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                RecordApiFailure(requestUrl, ex);
                AppLogger.Warn("NASA APOD API single async request failed for " + date.ToString("yyyy-MM-dd") + ".", ex);
                return await CreateEntryFromApodPageAsync(date).ConfigureAwait(false);
            }
        }

        public ApodEntry GetLatestEntry()
        {
            var requestUrl = BuildLatestRequestUrl();
            var retryProfile = ResolveApiRetryProfile();
            AppLogger.Web("source=nasa_api scope=latest url=" + requestUrl);
            try
            {
                var entry = Deserialize<ApodEntry>(Network.DownloadString(requestUrl, retryProfile), "NASA APOD API response could not be parsed.");
                entry.ResolvedFromSource = "api";
                return NormalizeEntry(entry, null);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("NASA APOD latest request failed.", ex);
                return GetLatestEntryFromPages();
            }
        }

        public async Task<ApodEntry> GetLatestEntryAsync()
        {
            var requestUrl = BuildLatestRequestUrl();
            var retryProfile = ResolveApiRetryProfile();
            AppLogger.Web("source=nasa_api scope=latest_async url=" + requestUrl);
            try
            {
                var entry = Deserialize<ApodEntry>(await Network.DownloadStringAsync(requestUrl, retryProfile).ConfigureAwait(false), "NASA APOD API response could not be parsed.");
                entry.ResolvedFromSource = "api";
                return await NormalizeEntryAsync(entry, null).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("NASA APOD latest async request failed.", ex);
                return await GetLatestEntryFromPagesAsync().ConfigureAwait(false);
            }
        }

        public IReadOnlyList<ApodEntry> GetEntries(DateTime startDate, DateTime endDate)
        {
            var requestUrl = BuildRangeRequestUrl(startDate, endDate);
            if (ShouldShortCircuitDemoDatedApi(requestUrl))
            {
                AppLogger.Web("source=nasa_api scope=range result=short_circuit_demo start=" + startDate.ToString("yyyy-MM-dd") + " end=" + endDate.ToString("yyyy-MM-dd"));
                return GetEntriesOneByOne(startDate, endDate);
            }

            var retryProfile = ResolveApiRetryProfile();
            AppLogger.Web("source=nasa_api scope=range start=" + startDate.ToString("yyyy-MM-dd") + " end=" + endDate.ToString("yyyy-MM-dd") + " url=" + requestUrl);
            try
            {
                var entries = Deserialize<List<ApodEntry>>(Network.DownloadString(requestUrl, retryProfile), "NASA APOD API range response could not be parsed.");
                RecordApiSuccess(requestUrl);
                return entries.Select(entry => NormalizeEntry(entry, null)).ToList();
            }
            catch (Exception ex)
            {
                RecordApiFailure(requestUrl, ex);
                AppLogger.Warn("NASA APOD range request failed for " + startDate.ToString("yyyy-MM-dd") + " - " + endDate.ToString("yyyy-MM-dd") + ".", ex);
                return GetEntriesOneByOne(startDate, endDate);
            }
        }

        public async Task<IReadOnlyList<ApodEntry>> GetEntriesAsync(DateTime startDate, DateTime endDate)
        {
            var requestUrl = BuildRangeRequestUrl(startDate, endDate);
            if (ShouldShortCircuitDemoDatedApi(requestUrl))
            {
                AppLogger.Web("source=nasa_api scope=range_async result=short_circuit_demo start=" + startDate.ToString("yyyy-MM-dd") + " end=" + endDate.ToString("yyyy-MM-dd"));
                return await GetEntriesOneByOneAsync(startDate, endDate).ConfigureAwait(false);
            }

            var retryProfile = ResolveApiRetryProfile();
            AppLogger.Web("source=nasa_api scope=range_async start=" + startDate.ToString("yyyy-MM-dd") + " end=" + endDate.ToString("yyyy-MM-dd") + " url=" + requestUrl);
            try
            {
                var entries = Deserialize<List<ApodEntry>>(await Network.DownloadStringAsync(requestUrl, retryProfile).ConfigureAwait(false), "NASA APOD API range response could not be parsed.");
                RecordApiSuccess(requestUrl);
                var normalizedEntries = new List<ApodEntry>(entries.Count);
                foreach (var entry in entries)
                    normalizedEntries.Add(await NormalizeEntryAsync(entry, null).ConfigureAwait(false));

                return normalizedEntries;
            }
            catch (Exception ex)
            {
                RecordApiFailure(requestUrl, ex);
                AppLogger.Warn("NASA APOD async range request failed for " + startDate.ToString("yyyy-MM-dd") + " - " + endDate.ToString("yyyy-MM-dd") + ".", ex);
                return await GetEntriesOneByOneAsync(startDate, endDate).ConfigureAwait(false);
            }
        }

        public async Task<ApiKeyValidationState> ValidateApiKeyAsync(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey) || string.Equals(apiKey.Trim(), DemoApiKey, StringComparison.OrdinalIgnoreCase))
                return ApiKeyValidationState.Unknown;

            var requestUrl = string.Format(
                CultureInfo.InvariantCulture,
                "{0}?api_key={1}",
                Endpoint,
                Uri.EscapeDataString(apiKey.Trim()));

            AppLogger.Web("source=nasa_api scope=validate url=" + requestUrl);

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(requestUrl);
                request.Method = "GET";
                request.UserAgent = "apod_wallpaper/1.0";
                request.Timeout = 15000;
                request.ReadWriteTimeout = 15000;
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                request.Proxy = WebRequest.DefaultWebProxy;
                if (request.Proxy != null)
                    request.Proxy.Credentials = CredentialCache.DefaultCredentials;

                using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream ?? Stream.Null, Encoding.UTF8))
                {
                    var json = reader.ReadToEnd();
                    if (LooksLikeInvalidApiKeyResponse(json))
                        return ApiKeyValidationState.Invalid;

                    Deserialize<ApodEntry>(json, "NASA APOD API validation response could not be parsed.");
                    return ApiKeyValidationState.Valid;
                }
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse response)
            {
                var statusCode = (int)response.StatusCode;
                using (response)
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream ?? Stream.Null, Encoding.UTF8))
                {
                    var body = reader.ReadToEnd();
                    if (LooksLikeInvalidApiKeyResponse(body))
                        return ApiKeyValidationState.Invalid;
                }

                if (statusCode == 401 || statusCode == 403)
                    return ApiKeyValidationState.Invalid;

                AppLogger.Warn("NASA API key validation returned HTTP " + statusCode + ".", ex);
                return ApiKeyValidationState.Unknown;
            }
            catch (Exception ex)
            {
                AppLogger.Warn("NASA API key validation failed.", ex);
                return ApiKeyValidationState.Unknown;
            }
        }

        private static string BuildSingleRequestUrl(DateTime date)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}?api_key={1}&date={2}",
                Endpoint,
                Uri.EscapeDataString(ResolveApiKey()),
                Uri.EscapeDataString(date.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }

        private static string BuildRangeRequestUrl(DateTime startDate, DateTime endDate)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}?api_key={1}&start_date={2}&end_date={3}",
                Endpoint,
                Uri.EscapeDataString(ResolveApiKey()),
                Uri.EscapeDataString(startDate.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                Uri.EscapeDataString(endDate.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }

        private static string BuildLatestRequestUrl()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}?api_key={1}",
                Endpoint,
                Uri.EscapeDataString(ResolveApiKey()));
        }

        private static T Deserialize<T>(string json, string parseErrorMessage)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("NASA APOD API returned an empty response.");

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                var result = serializer.ReadObject(stream);
                if (result == null)
                    throw new InvalidOperationException(parseErrorMessage);

                return (T)result;
            }
        }

        private static ApodEntry NormalizeEntry(ApodEntry entry, DateTime? requestedDate)
        {
            if (entry == null)
                throw new InvalidOperationException("NASA APOD API response could not be parsed.");

            if (requestedDate.HasValue && string.IsNullOrWhiteSpace(entry.Date))
                entry.Date = requestedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            if (entry.HasImage)
            {
                entry.MediaType = "image";
                return entry;
            }

            var entryDate = ResolveEntryDate(entry, requestedDate);
            if (!entryDate.HasValue)
                return entry;

            TryEnrichFromApodPage(entry, entryDate.Value);
            return entry;
        }

        private static async Task<ApodEntry> NormalizeEntryAsync(ApodEntry entry, DateTime? requestedDate)
        {
            if (entry == null)
                throw new InvalidOperationException("NASA APOD API response could not be parsed.");

            if (requestedDate.HasValue && string.IsNullOrWhiteSpace(entry.Date))
                entry.Date = requestedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            if (entry.HasImage)
            {
                entry.MediaType = "image";
                return entry;
            }

            var entryDate = ResolveEntryDate(entry, requestedDate);
            if (!entryDate.HasValue)
                return entry;

            await TryEnrichFromApodPageAsync(entry, entryDate.Value).ConfigureAwait(false);
            return entry;
        }

        private static DateTime? ResolveEntryDate(ApodEntry entry, DateTime? requestedDate)
        {
            if (entry != null && !string.IsNullOrWhiteSpace(entry.Date))
            {
                DateTime parsedDate;
                if (DateTime.TryParse(entry.Date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsedDate))
                    return parsedDate.Date;
            }

            return requestedDate?.Date;
        }

        private static void TryEnrichFromApodPage(ApodEntry entry, DateTime date)
        {
            try
            {
                var pageUrl = ApodPageUrl.GetUrl(date);
                AppLogger.Web("source=apod_html scope=enrich date=" + date.ToString("yyyy-MM-dd") + " url=" + pageUrl);
                var pageHtml = Network.DownloadString(pageUrl);
                ApplyHtmlResolutionToEntry(entry, date, pageUrl, pageHtml, "enrich");
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Unable to enrich APOD entry from HTML page for " + date.ToString("yyyy-MM-dd") + ".", ex);
            }
        }

        private static async Task TryEnrichFromApodPageAsync(ApodEntry entry, DateTime date)
        {
            try
            {
                var pageUrl = ApodPageUrl.GetUrl(date);
                AppLogger.Web("source=apod_html scope=enrich_async date=" + date.ToString("yyyy-MM-dd") + " url=" + pageUrl);
                var pageHtml = await Network.DownloadStringAsync(pageUrl).ConfigureAwait(false);
                ApplyHtmlResolutionToEntry(entry, date, pageUrl, pageHtml, "enrich_async");
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Unable to enrich APOD entry from HTML page for " + date.ToString("yyyy-MM-dd") + ".", ex);
            }
        }

        private static string ResolveApiKey()
        {
            var environmentApiKey = Environment.GetEnvironmentVariable("NASA_APOD_API_KEY");
            if (!string.IsNullOrWhiteSpace(environmentApiKey))
                return environmentApiKey.Trim();

            var configuredApiKey = AppRuntimeSettings.NasaApiKey;
            if (!string.IsNullOrWhiteSpace(configuredApiKey))
                return configuredApiKey.Trim();

            return DemoApiKey;
        }

        private static bool LooksLikeInvalidApiKeyResponse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return false;

            var normalized = json.ToLowerInvariant();
            return normalized.Contains("api_key_invalid") ||
                   normalized.Contains("invalid api key") ||
                   normalized.Contains("an invalid api key was supplied") ||
                   normalized.Contains("\"error\"") && normalized.Contains("api key");
        }

        private static NetworkRetryProfile ResolveApiRetryProfile()
        {
            var apiKey = ResolveApiKey();
            return string.Equals(apiKey, DemoApiKey, StringComparison.OrdinalIgnoreCase)
                ? NetworkRetryProfile.NasaApiDemo
                : NetworkRetryProfile.NasaApiAuthenticated;
        }

        private static ApodEntry CreateEntryFromApodPage(DateTime date)
        {
            var pageUrl = ApodPageUrl.GetUrl(date);
            AppLogger.Web("source=apod_html scope=page_only date=" + date.ToString("yyyy-MM-dd") + " url=" + pageUrl);
            var pageHtml = Network.DownloadString(pageUrl);
            return CreateEntryFromResolvedMedia(date, pageUrl, pageHtml, "page_only");
        }

        private static async Task<ApodEntry> CreateEntryFromApodPageAsync(DateTime date)
        {
            var pageUrl = ApodPageUrl.GetUrl(date);
            AppLogger.Web("source=apod_html scope=page_only_async date=" + date.ToString("yyyy-MM-dd") + " url=" + pageUrl);
            var pageHtml = await Network.DownloadStringAsync(pageUrl).ConfigureAwait(false);
            return CreateEntryFromResolvedMedia(date, pageUrl, pageHtml, "page_only_async");
        }

        private static ApodEntry CreateVideoEntry(DateTime date, string videoUrl)
        {
            return new ApodEntry
            {
                Date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Url = videoUrl,
                HdUrl = null,
                MediaType = "video",
                ResolvedFromSource = "html_fallback",
                IsFallbackImage = false,
            };
        }

        private static void ApplyHtmlResolutionToEntry(ApodEntry entry, DateTime date, string pageUrl, string pageHtml, string scope)
        {
            ApodPageImageExtractor.ApodPageMediaResolution resolution;
            if (!ApodPageImageExtractor.TryResolveMedia(pageHtml, pageUrl, out resolution))
            {
                AppLogger.Web("source=apod_html scope=" + scope + " result=unknown date=" + date.ToString("yyyy-MM-dd"));
                return;
            }

            if (resolution.Kind == ApodPageImageExtractor.ApodPageMediaKind.Image)
            {
                if (!string.IsNullOrWhiteSpace(resolution.PreviewUrl))
                    entry.Url = resolution.PreviewUrl;

                if (!string.IsNullOrWhiteSpace(resolution.ImageUrl))
                    entry.HdUrl = resolution.ImageUrl;

                if (entry.HasImage)
                {
                    entry.MediaType = "image";
                    entry.ResolvedFromSource = "html_fallback";
                    entry.IsFallbackImage = true;
                    AppLogger.Web("source=apod_html scope=" + scope + " result=image date=" + date.ToString("yyyy-MM-dd") + " preview=" + (resolution.PreviewUrl ?? "<null>") + " image=" + (resolution.ImageUrl ?? "<null>"));
                }

                return;
            }

            if (resolution.Kind == ApodPageImageExtractor.ApodPageMediaKind.Unsupported)
            {
                entry.Url = resolution.MediaUrl;
                entry.HdUrl = null;
                entry.MediaType = "video";
                entry.ResolvedFromSource = "html_fallback";
                entry.IsFallbackImage = false;
                AppLogger.Web("source=apod_html scope=" + scope + " result=unsupported date=" + date.ToString("yyyy-MM-dd") + " url=" + (resolution.MediaUrl ?? "<null>"));
            }
        }

        private static ApodEntry CreateEntryFromResolvedMedia(DateTime date, string pageUrl, string pageHtml, string scope)
        {
            ApodPageImageExtractor.ApodPageMediaResolution resolution;
            if (!ApodPageImageExtractor.TryResolveMedia(pageHtml, pageUrl, out resolution))
            {
                AppLogger.Web("source=apod_html scope=" + scope + " result=unknown date=" + date.ToString("yyyy-MM-dd"));
                throw new InvalidOperationException("Unable to classify APOD page media for " + date.ToString("yyyy-MM-dd") + ".");
            }

            if (resolution.Kind == ApodPageImageExtractor.ApodPageMediaKind.Image)
            {
                AppLogger.Web("source=apod_html scope=" + scope + " result=image date=" + date.ToString("yyyy-MM-dd") + " preview=" + (resolution.PreviewUrl ?? "<null>") + " image=" + (resolution.ImageUrl ?? "<null>"));
                return new ApodEntry
                {
                    Date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Url = resolution.PreviewUrl,
                    HdUrl = resolution.ImageUrl,
                    MediaType = "image",
                    ResolvedFromSource = "html_fallback",
                    IsFallbackImage = true,
                };
            }

            if (resolution.Kind == ApodPageImageExtractor.ApodPageMediaKind.Unsupported)
            {
                AppLogger.Web("source=apod_html scope=" + scope + " result=unsupported date=" + date.ToString("yyyy-MM-dd") + " url=" + (resolution.MediaUrl ?? "<null>"));
                return CreateVideoEntry(date, resolution.MediaUrl);
            }

            AppLogger.Web("source=apod_html scope=" + scope + " result=unknown date=" + date.ToString("yyyy-MM-dd"));
            throw new InvalidOperationException("Unable to classify APOD page media for " + date.ToString("yyyy-MM-dd") + ".");
        }

        private static bool ShouldShortCircuitDemoDatedApi(string requestUrl)
        {
            if (!IsDemoDatedApiRequest(requestUrl))
                return false;

            lock (DemoDatedApiSyncRoot)
            {
                if (string.Equals(ResolveApiKey(), DemoApiKey, StringComparison.OrdinalIgnoreCase))
                {
                    AppLogger.Web("source=nasa_api scope=demo_guard result=demo_html_only");
                    return true;
                }

                if (_demoDatedApiBlockedUntilUtc <= DateTime.UtcNow)
                    return false;

                AppLogger.Web("source=nasa_api scope=demo_guard result=blocked until=" + _demoDatedApiBlockedUntilUtc.ToString("o"));
                return true;
            }
        }

        private static void RecordApiSuccess(string requestUrl)
        {
            if (!IsDemoDatedApiRequest(requestUrl))
                return;

            lock (DemoDatedApiSyncRoot)
            {
                _demoDatedApi403Count = 0;
                _demoDatedApiBlockedUntilUtc = DateTime.MinValue;
            }
        }

        private static void RecordApiFailure(string requestUrl, Exception exception)
        {
            if (!IsDemoDatedApiRequest(requestUrl))
                return;

            int statusCode;
            if (!Network.TryGetHttpStatusCode(exception, out statusCode))
                return;

            lock (DemoDatedApiSyncRoot)
            {
                if (statusCode == 403)
                {
                    _demoDatedApi403Count++;
                    if (_demoDatedApi403Count >= DemoDatedApi403Threshold)
                    {
                        _demoDatedApiBlockedUntilUtc = DateTime.UtcNow.Add(DemoDatedApiBlockDuration);
                        AppLogger.Warn("DEMO_KEY dated NASA API temporarily blocked after repeated HTTP 403 responses. Falling back to APOD HTML pages until " + _demoDatedApiBlockedUntilUtc.ToString("o") + ".", exception);
                    }

                    return;
                }

                if (statusCode < 500)
                    _demoDatedApi403Count = 0;
            }
        }

        private static bool IsDemoDatedApiRequest(string requestUrl)
        {
            if (!string.Equals(ResolveApiKey(), DemoApiKey, StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.IsNullOrWhiteSpace(requestUrl))
                return false;

            return requestUrl.IndexOf("?api_key=", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   (requestUrl.IndexOf("&date=", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    requestUrl.IndexOf("&start_date=", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    requestUrl.IndexOf("&end_date=", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private IReadOnlyList<ApodEntry> GetEntriesOneByOne(DateTime startDate, DateTime endDate)
        {
            var entries = new List<ApodEntry>();
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                try
                {
                    entries.Add(GetEntry(date));
                }
                catch (Exception ex)
                {
                    AppLogger.Warn("Fallback single-day APOD fetch failed for " + date.ToString("yyyy-MM-dd") + ".", ex);
                }
            }

            return entries;
        }

        private async Task<IReadOnlyList<ApodEntry>> GetEntriesOneByOneAsync(DateTime startDate, DateTime endDate)
        {
            var entries = new List<ApodEntry>();
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                try
                {
                    entries.Add(await GetEntryAsync(date).ConfigureAwait(false));
                }
                catch (Exception ex)
                {
                    AppLogger.Warn("Async fallback single-day APOD fetch failed for " + date.ToString("yyyy-MM-dd") + ".", ex);
                }
            }

            return entries;
        }

        private ApodEntry GetLatestEntryFromPages()
        {
            foreach (var candidateDate in GetLatestFallbackDates())
            {
                try
                {
                    return CreateEntryFromApodPage(candidateDate);
                }
                catch (Exception ex)
                {
                    AppLogger.Warn("Latest APOD page fallback failed for " + candidateDate.ToString("yyyy-MM-dd") + ".", ex);
                }
            }

            throw new InvalidOperationException("Unable to resolve the latest APOD entry from NASA API or APOD pages.");
        }

        private async Task<ApodEntry> GetLatestEntryFromPagesAsync()
        {
            foreach (var candidateDate in GetLatestFallbackDates())
            {
                try
                {
                    return await CreateEntryFromApodPageAsync(candidateDate).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    AppLogger.Warn("Latest APOD async page fallback failed for " + candidateDate.ToString("yyyy-MM-dd") + ".", ex);
                }
            }

            throw new InvalidOperationException("Unable to resolve the latest APOD entry from NASA API or APOD pages.");
        }

        private static IEnumerable<DateTime> GetLatestFallbackDates()
        {
            var localToday = DateTime.Now.Date;
            var utcToday = DateTime.UtcNow.Date;
            var yieldedDates = new HashSet<DateTime>();

            for (var offset = 0; offset <= LatestFallbackLookback.TotalDays; offset++)
            {
                var localCandidate = localToday.AddDays(-offset);
                if (yieldedDates.Add(localCandidate))
                    yield return localCandidate;

                var utcCandidate = utcToday.AddDays(-offset);
                if (yieldedDates.Add(utcCandidate))
                    yield return utcCandidate;
            }
        }
    }
}
