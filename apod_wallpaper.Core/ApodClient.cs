using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    internal sealed class ApodClient : IApodClient
    {
        private readonly IApodClient _source;
        internal bool UsesScienceSource => _source is ApodScienceClient;

        // Keep the production source unchanged until sync transport and cache migration are ready.
        public ApodClient() : this(new LegacyApodClient()) { }
        internal ApodClient(IApodClient source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _source = source;
        }
        public ApodEntry GetEntry(DateTime date) => _source.GetEntry(date);
        public Task<ApodEntry> GetEntryAsync(DateTime date) => _source.GetEntryAsync(date);
        public ApodEntry GetLatestEntry() => _source.GetLatestEntry();
        public Task<ApodEntry> GetLatestEntryAsync() => _source.GetLatestEntryAsync();
        public IReadOnlyList<ApodEntry> GetEntries(DateTime startDate, DateTime endDate) => _source.GetEntries(startDate, endDate);
        public Task<IReadOnlyList<ApodEntry>> GetEntriesAsync(DateTime startDate, DateTime endDate) => _source.GetEntriesAsync(startDate, endDate);
        public Task<ApiKeyValidationState> ValidateApiKeyAsync(string apiKey) => _source.ValidateApiKeyAsync(apiKey);
    }
}
