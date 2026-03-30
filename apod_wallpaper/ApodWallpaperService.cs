using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace apod_wallpaper
{
    internal sealed class ApodWallpaperService
    {
        private static readonly TimeSpan LatestEntryCacheDuration = TimeSpan.FromHours(2);
        private static readonly TimeSpan HousekeepingInterval = TimeSpan.FromHours(6);
        private static DateTime _lastHousekeepingUtc = DateTime.MinValue;
        private static readonly object HousekeepingSyncRoot = new object();
        private readonly ApodClient _client = new ApodClient();
        private readonly ApodMetadataCache _cache = new ApodMetadataCache();
        private readonly WallpaperService _wallpaperService = new WallpaperService();
        private ApodEntry _latestEntry;
        private DateTime _latestEntryFetchedAtUtc;

        public ApodWallpaperService()
        {
            RunHousekeepingIfNeeded();
        }

        public ApodEntry GetEntry(DateTime date, bool forceRefresh = false)
        {
            var cached = !forceRefresh ? _cache.Get(date) : null;
            if (cached != null)
            {
                var cachedEntry = cached.ToEntry();
                if (cachedEntry.HasImage)
                    return cachedEntry;
            }

            var entry = _client.GetEntry(date);
            _cache.Upsert(entry);
            AppLogger.Info("Resolved APOD " + date.ToString("yyyy-MM-dd") + " from " + (entry.ResolvedFromSource ?? "unknown") + ".");
            return entry;
        }

        public ApodEntry GetLatestEntry(bool forceRefresh = false)
        {
            if (!forceRefresh &&
                _latestEntry != null &&
                DateTime.UtcNow - _latestEntryFetchedAtUtc < LatestEntryCacheDuration)
            {
                return _latestEntry;
            }

            var latestEntry = _client.GetLatestEntry();
            var entryDate = DateTime.Parse(latestEntry.Date).Date;

            if (!forceRefresh)
            {
                var cached = _cache.Get(entryDate);
                if (cached != null)
                {
                    var cachedEntry = cached.ToEntry();
                    if (cachedEntry.HasImage || !ShouldRefreshCachedEntry(cached, entryDate))
                    {
                        _latestEntry = cachedEntry;
                        _latestEntryFetchedAtUtc = DateTime.UtcNow;
                        return cachedEntry;
                    }
                }
            }

            _cache.Upsert(latestEntry);
            _latestEntry = latestEntry;
            _latestEntryFetchedAtUtc = DateTime.UtcNow;
            AppLogger.Info("Resolved latest APOD " + latestEntry.Date + " from " + (latestEntry.ResolvedFromSource ?? "unknown") + ".");
            return latestEntry;
        }

        public IReadOnlyList<ApodDayAvailability> GetMonthAvailability(DateTime month, bool refreshMissingDates)
        {
            var monthStart = new DateTime(month.Year, month.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var latestAvailableDate = GetLatestAvailableDate();
            var fetchEnd = monthEnd <= latestAvailableDate ? monthEnd : latestAvailableDate;

            if (refreshMissingDates && fetchEnd >= monthStart)
            {
                var cachedEntriesForRange = _cache
                    .GetRange(monthStart, fetchEnd)
                    .ToDictionary(item => DateTime.Parse(item.Date).Date, item => item);

                var shouldRefreshRange = false;
                for (var date = monthStart; date <= fetchEnd; date = date.AddDays(1))
                {
                    ApodCachedEntry cachedEntry;
                    if (!cachedEntriesForRange.TryGetValue(date, out cachedEntry))
                    {
                        shouldRefreshRange = true;
                        break;
                    }

                    if (!cachedEntry.ToEntry().HasImage && ShouldRefreshCachedEntry(cachedEntry, date))
                    {
                        shouldRefreshRange = true;
                        break;
                    }
                }

                if (shouldRefreshRange)
                {
                    var entries = _client.GetEntries(monthStart, fetchEnd);
                    _cache.UpsertRange(entries);
                    AppLogger.Info("Refreshed APOD month availability for " + monthStart.ToString("yyyy-MM") + ".");
                }
            }

            var cachedEntries = _cache.GetRange(monthStart, monthEnd)
                .ToDictionary(item => DateTime.Parse(item.Date).Date, item => item);

            var result = new List<ApodDayAvailability>();
            for (var date = monthStart; date <= monthEnd; date = date.AddDays(1))
            {
                ApodCachedEntry cachedEntry;
                if (cachedEntries.TryGetValue(date, out cachedEntry))
                {
                    var entry = cachedEntry.ToEntry();
                    result.Add(new ApodDayAvailability
                    {
                        Date = date,
                        IsKnown = true,
                        HasImage = entry.HasImage,
                        MediaType = entry.MediaType,
                    });
                }
                else
                {
                    result.Add(new ApodDayAvailability
                    {
                        Date = date,
                        IsKnown = false,
                        HasImage = false,
                        MediaType = null,
                    });
                }
            }

            return result;
        }

        public ApodPreviewResult Preview(DateTime date, bool forceRefresh = false)
        {
            var entry = GetEntry(date, forceRefresh);
            var localImagePath = TryGetLocalImagePath(date);

            return new ApodPreviewResult
            {
                Entry = entry,
                PreviewLocation = localImagePath ?? entry.PreviewImageUrl,
                IsLocalFile = !string.IsNullOrWhiteSpace(localImagePath),
                PostUrl = OpenPost(date),
            };
        }

        public ApodDownloadResult Download(DateTime date, bool forceRefresh = false)
        {
            var entry = GetEntry(date, forceRefresh);
            return EnsureImageDownloaded(entry, date);
        }

        public ApodApplyResult Apply(DateTime date, WallpaperStyle style, bool forceRefresh = false)
        {
            var downloadResult = Download(date, forceRefresh);
            _wallpaperService.ApplyPreservingHistory(downloadResult.ImagePath, style);

            return new ApodApplyResult
            {
                Entry = downloadResult.Entry,
                ImagePath = downloadResult.ImagePath,
                DownloadedNow = downloadResult.DownloadedNow,
            };
        }

        public ApodApplyResult ApplyLatestAvailable(WallpaperStyle style, bool forceRefresh = false)
        {
            var latestEntry = GetLatestEntry(forceRefresh);
            var entryDate = DateTime.Parse(latestEntry.Date).Date;
            var downloadResult = EnsureImageDownloaded(latestEntry, entryDate);
            _wallpaperService.ApplyPreservingHistory(downloadResult.ImagePath, style);

            return new ApodApplyResult
            {
                Entry = downloadResult.Entry,
                ImagePath = downloadResult.ImagePath,
                DownloadedNow = downloadResult.DownloadedNow,
            };
        }

        public string OpenPost(DateTime date)
        {
            return TodayUrl.GetUrl(date);
        }

        public ApodDownloadResult EnsureImageDownloaded(ApodEntry entry, DateTime date)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (!entry.HasImage || string.IsNullOrWhiteSpace(entry.BestImageUrl))
                throw new InvalidOperationException("The selected APOD entry does not contain a downloadable image.");

            FileStorage.EnsureImagesDirectory();

            var baseName = TodayUrl.GetBaseName(date);
            var existingImagePath = FileStorage.TryFindExistingImagePath(baseName);
            if (!string.IsNullOrWhiteSpace(existingImagePath) && File.Exists(existingImagePath))
            {
                _cache.Upsert(entry);
                _cache.SaveLocalImagePath(date, existingImagePath);

                return new ApodDownloadResult
                {
                    Entry = entry,
                    ImagePath = existingImagePath,
                    DownloadedNow = false,
                };
            }

            var image = new Image(entry.BestImageUrl, baseName);
            var downloadedNow = false;
            if (!File.Exists(image.FullPath))
            {
                image.DownloadImage();
                image.SaveImage();
                downloadedNow = true;
            }

            _cache.Upsert(entry);
            _cache.SaveLocalImagePath(date, image.FullPath);
            AppLogger.Info("Stored APOD image for " + date.ToString("yyyy-MM-dd") + " at " + image.FullPath + ".");

            return new ApodDownloadResult
            {
                Entry = entry,
                ImagePath = image.FullPath,
                DownloadedNow = downloadedNow,
            };
        }

        private string TryGetLocalImagePath(DateTime date)
        {
            var baseName = TodayUrl.GetBaseName(date);
            var expectedPath = FileStorage.TryFindExistingImagePath(baseName);
            if (!string.IsNullOrWhiteSpace(expectedPath) && File.Exists(expectedPath))
            {
                _cache.SaveLocalImagePath(date, expectedPath);
                return expectedPath;
            }

            var cached = _cache.Get(date);
            if (cached != null && !string.IsNullOrWhiteSpace(cached.LocalImagePath) && File.Exists(cached.LocalImagePath))
                return cached.LocalImagePath;

            return null;
        }

        public DateTime GetLatestAvailableDate()
        {
            try
            {
                var latestEntry = GetLatestEntry();
                return DateTime.Parse(latestEntry.Date).Date;
            }
            catch
            {
                return DateTime.UtcNow.Date;
            }
        }

        private static bool ShouldRefreshCachedEntry(ApodCachedEntry cached, DateTime requestedDate)
        {
            if (cached == null)
                return true;

            if (cached.CachedAtUtc == default(DateTime))
                return true;

            var cacheAge = DateTime.UtcNow - cached.CachedAtUtc;
            var cachedEntry = cached.ToEntry();
            if (!cachedEntry.HasImage)
            {
                if (requestedDate.Date >= DateTime.UtcNow.Date.AddDays(-3))
                    return cacheAge >= TimeSpan.FromHours(2);

                return cacheAge >= TimeSpan.FromHours(12);
            }

            if (requestedDate.Date >= DateTime.UtcNow.Date.AddDays(-3))
                return cacheAge >= TimeSpan.FromHours(2);

            return cacheAge >= TimeSpan.FromDays(7);
        }

        private void RunHousekeepingIfNeeded()
        {
            lock (HousekeepingSyncRoot)
            {
                if (DateTime.UtcNow - _lastHousekeepingUtc < HousekeepingInterval)
                    return;

                try
                {
                    _cache.SyncLocalImagePaths();
                    _lastHousekeepingUtc = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    AppLogger.Warn("Background cache housekeeping failed.", ex);
                }
            }
        }
    }
}
