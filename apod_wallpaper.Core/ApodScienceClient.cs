using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    // Staged adapter; do not select as production default before NASA-05/06/07 are complete.
    internal sealed class ApodScienceClient : IApodClient
    {
        private readonly ApodScienceSource _source;
        private readonly IApodClient _legacy;

        internal ApodScienceClient(ApodScienceSource source, IApodClient legacy)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
        }

        public ApodEntry GetEntry(DateTime date) => _source.GetEntry(date).Entry;
        public async Task<ApodEntry> GetEntryAsync(DateTime date)
        {
            return (await _source.GetEntryAsync(date).ConfigureAwait(false)).Entry;
        }

        // These operations migrate separately; a failed Science date never falls back to legacy.
        public ApodEntry GetLatestEntry() => _legacy.GetLatestEntry();
        public Task<ApodEntry> GetLatestEntryAsync() => _legacy.GetLatestEntryAsync();
        public IReadOnlyList<ApodEntry> GetEntries(DateTime startDate, DateTime endDate) => _legacy.GetEntries(startDate, endDate);
        public Task<IReadOnlyList<ApodEntry>> GetEntriesAsync(DateTime startDate, DateTime endDate) => _legacy.GetEntriesAsync(startDate, endDate);
        public Task<ApiKeyValidationState> ValidateApiKeyAsync(string apiKey) => _legacy.ValidateApiKeyAsync(apiKey);
    }
}
