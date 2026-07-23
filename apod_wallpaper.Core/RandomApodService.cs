using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    internal sealed class RandomApodService
    {
        internal static readonly DateTime DefaultGlobalStartDate = new DateTime(2015, 1, 1);
        internal static readonly DateTime DeepArchiveStartDate = new DateTime(1995, 6, 16);
        internal const int GlobalAvailabilityAttemptLimit = 10;

        private static readonly Random SharedRandom = new Random();
        private static readonly object RandomSyncRoot = new object();
        private readonly ApodPageAvailabilityProbe _pageAvailabilityProbe;

        public RandomApodService(ApodPageAvailabilityProbe pageAvailabilityProbe)
        {
            _pageAvailabilityProbe = pageAvailabilityProbe ?? throw new ArgumentNullException(nameof(pageAvailabilityProbe));
        }

        public async Task<RandomApodResult> PickGlobalAsync(bool includeDeepArchive)
        {
            var startDate = ResolveGlobalStartDate(includeDeepArchive);
            var endDate = DateTime.Today.Date;
            if (startDate > endDate)
                return RandomApodResult.NoCandidates(RandomApodSource.Global, includeDeepArchive, "Random APOD date range is empty.");

            for (var attempt = 1; attempt <= GlobalAvailabilityAttemptLimit; attempt++)
            {
                var candidate = PickDateInRange(startDate, endDate);
                var probe = await _pageAvailabilityProbe.ProbeAsync(candidate, TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                if (probe.IsAvailable)
                    return RandomApodResult.Success(candidate, RandomApodSource.Global, includeDeepArchive, attempt);
            }

            return RandomApodResult.Unavailable(
                RandomApodSource.Global,
                includeDeepArchive,
                GlobalAvailabilityAttemptLimit,
                "Could not find an available APOD date quickly.");
        }

        public RandomApodResult PickFromKnownDates(IReadOnlyList<DateTime> dates, string source, bool includeDeepArchive)
        {
            var normalizedSource = RandomApodSource.Normalize(source);
            var candidates = NormalizeCandidateDates(dates);
            if (candidates.Count == 0)
                return RandomApodResult.NoCandidates(normalizedSource, includeDeepArchive, "No APOD images are available for this random source.");

            return RandomApodResult.Success(candidates[PickIndex(candidates.Count)], normalizedSource, includeDeepArchive, 1);
        }

        internal static DateTime ResolveGlobalStartDate(bool includeDeepArchive)
        {
            return includeDeepArchive ? DeepArchiveStartDate : DefaultGlobalStartDate;
        }

        internal static IReadOnlyList<DateTime> NormalizeCandidateDates(IEnumerable<DateTime> dates)
        {
            if (dates == null)
                return Array.Empty<DateTime>();

            return dates
                .Select(date => date.Date)
                .Where(date => date <= DateTime.Today.Date)
                .Distinct()
                .OrderBy(date => date)
                .ToList();
        }

        private static DateTime PickDateInRange(DateTime startDate, DateTime endDate)
        {
            var totalDays = (endDate.Date - startDate.Date).Days + 1;
            return startDate.Date.AddDays(PickIndex(totalDays));
        }

        private static int PickIndex(int exclusiveUpperBound)
        {
            lock (RandomSyncRoot)
            {
                return SharedRandom.Next(exclusiveUpperBound);
            }
        }
    }
}
