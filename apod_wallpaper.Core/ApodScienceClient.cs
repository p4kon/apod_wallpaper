using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    // Staged adapter; do not select as production default before NASA-05/06/07 are complete.
    internal sealed class ApodScienceClient : IApodClient
    {
        private readonly ApodScienceSource _source;
        private readonly IApodClient _legacy;
        private readonly Func<DateTime> _latestDate;
        private readonly TimeSpan _latestBudget;

        internal ApodScienceClient(ApodScienceSource source, IApodClient legacy,
            Func<DateTime> latestDate = null, TimeSpan? latestBudget = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
            _latestDate = latestDate ?? (() => DateTime.Today > DateTime.UtcNow.Date ? DateTime.Today : DateTime.UtcNow.Date);
            _latestBudget = latestBudget ?? TimeSpan.FromSeconds(8);
        }

        public ApodEntry GetEntry(DateTime date) => _source.GetEntry(date).Entry;
        public async Task<ApodEntry> GetEntryAsync(DateTime date)
        {
            return (await _source.GetEntryAsync(date).ConfigureAwait(false)).Entry;
        }

        public ApodEntry GetLatestEntry()
        {
            using (var budget = new CancellationTokenSource(_latestBudget))
            {
                try
                {
                    foreach (var date in LatestCandidates())
                    {
                        try { return _source.GetEntry(date, budget.Token).Entry; }
                        catch (ApodEntryUnavailableException) { }
                    }
                    throw new ApodEntryUnavailableException(_latestDate(), "No recent NASA Science publication was found.");
                }
                catch (OperationCanceledException ex) when (budget.IsCancellationRequested)
                { throw new TimeoutException("NASA Science latest lookup timed out.", ex); }
            }
        }

        public async Task<ApodEntry> GetLatestEntryAsync()
        {
            using (var budget = new CancellationTokenSource(_latestBudget))
            {
                try
                {
                    foreach (var date in LatestCandidates())
                    {
                        try { return (await _source.GetEntryAsync(date, budget.Token).ConfigureAwait(false)).Entry; }
                        catch (ApodEntryUnavailableException) { }
                    }
                    throw new ApodEntryUnavailableException(_latestDate(), "No recent NASA Science publication was found.");
                }
                catch (OperationCanceledException ex) when (budget.IsCancellationRequested)
                { throw new TimeoutException("NASA Science latest lookup timed out.", ex); }
            }
        }

        private IEnumerable<DateTime> LatestCandidates()
        {
            var today = _latestDate().Date;
            for (var offset = 0; offset <= 3; offset++)
            {
                var date = today.AddDays(-offset);
                if (date < RandomApodService.DeepArchiveStartDate) yield break;
                yield return date;
            }
        }

        // Range migration is separate; errors from Science never trigger legacy retries.
        public IReadOnlyList<ApodEntry> GetEntries(DateTime startDate, DateTime endDate) => _legacy.GetEntries(startDate, endDate);
        public Task<IReadOnlyList<ApodEntry>> GetEntriesAsync(DateTime startDate, DateTime endDate) => _legacy.GetEntriesAsync(startDate, endDate);
        public Task<ApiKeyValidationState> ValidateApiKeyAsync(string apiKey) => _legacy.ValidateApiKeyAsync(apiKey);
    }
}
