using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Linq;

namespace apod_wallpaper
{
    internal sealed class ApodClient
    {
        private const string DemoApiKey = "DEMO_KEY";
        private const string Endpoint = "https://api.nasa.gov/planetary/apod";

        public ApodEntry GetEntry(DateTime date)
        {
            var requestUrl = BuildSingleRequestUrl(date);
            var entry = Deserialize<ApodEntry>(Network.DownloadString(requestUrl), "NASA APOD API response could not be parsed.");
            entry.ResolvedFromSource = "api";
            return NormalizeEntry(entry, date);
        }

        public ApodEntry GetLatestEntry()
        {
            var requestUrl = BuildLatestRequestUrl();
            var entry = Deserialize<ApodEntry>(Network.DownloadString(requestUrl), "NASA APOD API response could not be parsed.");
            entry.ResolvedFromSource = "api";
            return NormalizeEntry(entry, null);
        }

        public IReadOnlyList<ApodEntry> GetEntries(DateTime startDate, DateTime endDate)
        {
            var requestUrl = BuildRangeRequestUrl(startDate, endDate);
            var entries = Deserialize<List<ApodEntry>>(Network.DownloadString(requestUrl), "NASA APOD API range response could not be parsed.");
            return entries.Select(entry => NormalizeEntry(entry, null)).ToList();
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
                var pageUrl = TodayUrl.GetUrl(date);
                var pageHtml = Network.DownloadString(pageUrl);

                string previewUrl;
                string imageUrl;
                if (!ApodPageImageExtractor.TryExtract(pageHtml, pageUrl, out previewUrl, out imageUrl))
                    return;

                if (!string.IsNullOrWhiteSpace(previewUrl))
                    entry.Url = previewUrl;

                if (!string.IsNullOrWhiteSpace(imageUrl))
                    entry.HdUrl = imageUrl;

                if (entry.HasImage)
                {
                    entry.MediaType = "image";
                    entry.ResolvedFromSource = "html_fallback";
                    entry.IsFallbackImage = true;
                }
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
    }
}
