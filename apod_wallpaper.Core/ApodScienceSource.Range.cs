using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    internal sealed partial class ApodScienceSource
    {
        internal IReadOnlyList<ApodEntry> GetEntries(DateTime start, DateTime end)
        {
            var range = new RangeCollector(start, end);
            using (var budget = new CancellationTokenSource(_timeout))
            {
                try
                {
                    do
                    {
                        var json = ReadJson(start, budget.Token, range.NextUri(), range.ReadHeaders);
                        range.Add(json);
                    } while (range.HasNext);
                    return range.Finish();
                }
                catch (OperationCanceledException ex) when (budget.IsCancellationRequested)
                { throw new TimeoutException("NASA Science range lookup timed out.", ex); }
                catch (ApodEntryUnavailableException ex)
                { throw new InvalidDataException("NASA Science range endpoint was unavailable; no dates were classified.", ex); }
            }
        }

        internal async Task<IReadOnlyList<ApodEntry>> GetEntriesAsync(DateTime start, DateTime end)
        {
            var range = new RangeCollector(start, end);
            using (var budget = new CancellationTokenSource(_timeout))
            {
                try
                {
                    do
                    {
                        var json = await ReadJsonAsync(start, budget.Token, range.NextUri(), range.ReadHeaders).ConfigureAwait(false);
                        range.Add(json);
                    } while (range.HasNext);
                    return range.Finish();
                }
                catch (OperationCanceledException ex) when (budget.IsCancellationRequested)
                { throw new TimeoutException("NASA Science range lookup timed out.", ex); }
                catch (ApodEntryUnavailableException ex)
                { throw new InvalidDataException("NASA Science range endpoint was unavailable; no dates were classified.", ex); }
            }
        }

        private sealed class RangeCollector
        {
            private readonly DateTime _start;
            private readonly DateTime _end;
            private readonly Dictionary<string, ApodEntry> _entries = new Dictionary<string, ApodEntry>(StringComparer.Ordinal);
            private int _page = 1;
            private int? _total;
            private int _pages;

            internal RangeCollector(DateTime start, DateTime end)
            {
                _start = start.Date;
                _end = end.Date;
                if (_end < _start || (_end - _start).TotalDays >= 366)
                    throw new ArgumentOutOfRangeException(nameof(end), "NASA Science ranges must contain 1 to 366 dates.");
            }

            internal bool HasNext => _page <= _pages;

            internal Uri NextUri()
            {
                return new Uri("https://science.nasa.gov/wp-json/wp/v2/apod-basic?date_from="
                    + _start.ToString("yyMMdd", CultureInfo.InvariantCulture) + "&date_to="
                    + _end.ToString("yyMMdd", CultureInfo.InvariantCulture) + "&page=" + _page.ToString(CultureInfo.InvariantCulture));
            }

            internal void ReadHeaders(ApodScienceResponse response)
            {
                if (!response.Total.HasValue || !response.TotalPages.HasValue
                    || response.Total < 0 || response.Total > (_end - _start).Days + 1
                    || response.TotalPages < 0 || response.TotalPages > 32
                    || (response.Total > 0 && response.TotalPages == 0)
                    || (response.Total == 0 && response.TotalPages > 1))
                    throw new InvalidDataException("Invalid NASA Science pagination headers.");
                if (_total.HasValue && (_total != response.Total || _pages != response.TotalPages))
                    throw new InvalidDataException("NASA Science pagination changed during the request.");
                _total = response.Total;
                _pages = response.TotalPages.Value;
            }

            internal void Add(string json)
            {
                var entries = ApodScienceParser.ParseRangePage(json, _start, _end);
                if (entries.Count == 0 && _total > 0)
                    throw new InvalidDataException("NASA Science returned an incomplete page.");
                foreach (var entry in entries)
                {
                    if (_entries.TryGetValue(entry.Date, out var previous))
                    {
                        if (previous.Url != entry.Url || previous.HdUrl != entry.HdUrl || previous.MediaType != entry.MediaType
                            || previous.PostUrl != entry.PostUrl || previous.Title != entry.Title || previous.Explanation != entry.Explanation
                            || previous.Copyright != entry.Copyright || previous.SourcePreviewUrl != entry.SourcePreviewUrl || previous.SourceOriginalUrl != entry.SourceOriginalUrl)
                            throw new InvalidDataException("NASA Science returned conflicting entries for one date.");
                    }
                    else _entries.Add(entry.Date, entry);
                }
                _page++;
            }

            internal IReadOnlyList<ApodEntry> Finish()
            {
                if (_entries.Count != _total)
                    throw new InvalidDataException("NASA Science range is incomplete.");
                return _entries.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value).ToList();
            }
        }
    }
}
