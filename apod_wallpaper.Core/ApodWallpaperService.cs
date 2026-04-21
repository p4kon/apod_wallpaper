using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    internal sealed class ApodWallpaperService
    {
        private static readonly TimeSpan LatestEntryCacheDuration = TimeSpan.FromHours(2);
        private static readonly TimeSpan HousekeepingInterval = TimeSpan.FromHours(6);
        private static readonly TimeSpan LatestImageLookback = TimeSpan.FromDays(7);
        private static DateTime _lastHousekeepingUtc = DateTime.MinValue;
        private static readonly object HousekeepingSyncRoot = new object();
        private readonly IApodClient _client;
        private readonly IApodMetadataCache _cache;
        private readonly IWallpaperApplier _wallpaperService;
        private ApodEntry _latestEntry;
        private DateTime _latestEntryFetchedAtUtc;

        public ApodWallpaperService()
            : this(new ApodClient(), new ApodMetadataCache(), new WallpaperService())
        {
        }

        internal ApodWallpaperService(IApodClient client, IApodMetadataCache cache, IWallpaperApplier wallpaperService)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _wallpaperService = wallpaperService ?? throw new ArgumentNullException(nameof(wallpaperService));
            RunHousekeepingIfNeeded();
        }

        public ApodEntry GetEntry(DateTime date, bool forceRefresh = false)
        {
            return GetEntryByDate(date, forceRefresh);
        }

        public ApodEntry GetEntryByDate(DateTime date, bool forceRefresh = false)
        {
            var localImagePath = TryGetLocalImagePath(date);
            var cached = !forceRefresh ? _cache.Get(date) : null;
            if (cached != null)
            {
                var cachedEntry = cached.ToEntry();
                var hasUsableLocalImage = LocalImageValidator.IsUsableImageFile(localImagePath);
                if (cachedEntry.HasImage || hasUsableLocalImage)
                {
                    cachedEntry.ResolvedFromSource = hasUsableLocalImage ? "local_file" : "cache";
                    return cachedEntry;
                }
            }

            if (!forceRefresh && LocalImageValidator.IsUsableImageFile(localImagePath))
            {
                return CreateLocalFileEntry(date);
            }

            var entry = _client.GetEntry(date);
            _cache.Upsert(entry);
            AppLogger.Info("Resolved APOD " + date.ToString("yyyy-MM-dd") + " from " + (entry.ResolvedFromSource ?? "unknown") + ".");
            return entry;
        }

        public async Task<ApodEntry> GetEntryByDateAsync(DateTime date, bool forceRefresh = false)
        {
            var localImagePath = TryGetLocalImagePath(date);
            var cached = !forceRefresh ? _cache.Get(date) : null;
            if (cached != null)
            {
                var cachedEntry = cached.ToEntry();
                var hasUsableLocalImage = LocalImageValidator.IsUsableImageFile(localImagePath);
                if (cachedEntry.HasImage || hasUsableLocalImage)
                {
                    cachedEntry.ResolvedFromSource = hasUsableLocalImage ? "local_file" : "cache";
                    return cachedEntry;
                }
            }

            if (!forceRefresh && LocalImageValidator.IsUsableImageFile(localImagePath))
            {
                return CreateLocalFileEntry(date);
            }

            var entry = await _client.GetEntryAsync(date).ConfigureAwait(false);
            _cache.Upsert(entry);
            AppLogger.Info("Resolved APOD " + date.ToString("yyyy-MM-dd") + " from " + (entry.ResolvedFromSource ?? "unknown") + ".");
            return entry;
        }

        public ApodEntry GetLatestEntry(bool forceRefresh = false)
        {
            return GetLatestPublishedEntry(forceRefresh);
        }

        public ApodEntry GetLatestPublishedEntry(bool forceRefresh = false)
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
                        cachedEntry.ResolvedFromSource = cachedEntry.HasImage ? "cache" : cachedEntry.ResolvedFromSource;
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

        public async Task<ApodEntry> GetLatestPublishedEntryAsync(bool forceRefresh = false)
        {
            if (!forceRefresh &&
                _latestEntry != null &&
                DateTime.UtcNow - _latestEntryFetchedAtUtc < LatestEntryCacheDuration)
            {
                return _latestEntry;
            }

            var latestEntry = await _client.GetLatestEntryAsync().ConfigureAwait(false);
            var entryDate = DateTime.Parse(latestEntry.Date).Date;

            if (!forceRefresh)
            {
                var cached = _cache.Get(entryDate);
                if (cached != null)
                {
                    var cachedEntry = cached.ToEntry();
                    if (cachedEntry.HasImage || !ShouldRefreshCachedEntry(cached, entryDate))
                    {
                        cachedEntry.ResolvedFromSource = cachedEntry.HasImage ? "cache" : cachedEntry.ResolvedFromSource;
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
            return GetMonthStatus(month, refreshMissingDates);
        }

        public IReadOnlyList<ApodDayAvailability> GetMonthStatus(DateTime month, bool refreshMissingDates)
        {
            return GetMonthStatus(month, refreshMissingDates, GetLatestPublishedDate(), MonthRefreshMode.Aggressive);
        }

        public IReadOnlyList<ApodDayAvailability> GetMonthStatus(DateTime month, bool refreshMissingDates, DateTime latestAvailableDate, MonthRefreshMode refreshMode)
        {
            var monthStart = new DateTime(month.Year, month.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
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
                    AppLogger.Web("scope=month_refresh month=" + monthStart.ToString("yyyy-MM") + " mode=sync refreshMissingDates=true");
                    RefreshMonthStatus(monthStart, fetchEnd, cachedEntriesForRange.Count, refreshMode);
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
                        IsLocalImageAvailable = HasLocalImage(date),
                        IsSelectable = entry.HasImage,
                        MediaType = entry.MediaType,
                        Source = ResolveDataSource(entry.ResolvedFromSource),
                    });
                }
                else
                {
                    result.Add(new ApodDayAvailability
                    {
                        Date = date,
                        IsKnown = false,
                        HasImage = false,
                        IsLocalImageAvailable = false,
                        IsSelectable = false,
                        MediaType = null,
                        Source = ApodDataSource.Unknown,
                    });
                }
            }

            return result;
        }

        public async Task<IReadOnlyList<ApodDayAvailability>> GetMonthStatusAsync(DateTime month, bool refreshMissingDates)
        {
            return await GetMonthStatusAsync(month, refreshMissingDates, await GetLatestPublishedDateAsync().ConfigureAwait(false), MonthRefreshMode.Aggressive).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<ApodDayAvailability>> GetMonthStatusAsync(DateTime month, bool refreshMissingDates, DateTime latestAvailableDate, MonthRefreshMode refreshMode)
        {
            var monthStart = new DateTime(month.Year, month.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
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
                    AppLogger.Web("scope=month_refresh month=" + monthStart.ToString("yyyy-MM") + " mode=async refreshMissingDates=true");
                    await RefreshMonthStatusAsync(monthStart, fetchEnd, cachedEntriesForRange.Count, refreshMode).ConfigureAwait(false);
                }
            }

            return BuildMonthStatus(monthStart, monthEnd);
        }

        public ApodPreviewResult Preview(DateTime date, bool forceRefresh = false)
        {
            return GetPreviewByDate(date, forceRefresh);
        }

        public ApodPreviewResult GetPreviewByDate(DateTime date, bool forceRefresh = false)
        {
            var entry = GetEntryByDate(date, forceRefresh);
            var localImagePath = entry.HasImage ? TryGetLocalImagePath(date) : null;

            return new ApodPreviewResult
            {
                Entry = entry,
                PreviewLocation = localImagePath ?? entry.PreviewImageUrl,
                IsLocalFile = !string.IsNullOrWhiteSpace(localImagePath),
                PostUrl = OpenPost(date),
                Source = !string.IsNullOrWhiteSpace(localImagePath)
                    ? ApodDataSource.LocalFile
                    : ResolveDataSource(entry.ResolvedFromSource),
            };
        }

        public async Task<ApodPreviewResult> GetPreviewByDateAsync(DateTime date, bool forceRefresh = false)
        {
            var entry = await GetEntryByDateAsync(date, forceRefresh).ConfigureAwait(false);
            var localImagePath = entry.HasImage ? TryGetLocalImagePath(date) : null;

            return new ApodPreviewResult
            {
                Entry = entry,
                PreviewLocation = localImagePath ?? entry.PreviewImageUrl,
                IsLocalFile = !string.IsNullOrWhiteSpace(localImagePath),
                PostUrl = OpenPost(date),
                Source = !string.IsNullOrWhiteSpace(localImagePath)
                    ? ApodDataSource.LocalFile
                    : ResolveDataSource(entry.ResolvedFromSource),
            };
        }

        public ApodDownloadResult Download(DateTime date, bool forceRefresh = false)
        {
            return DownloadImageByDate(date, forceRefresh);
        }

        public ApodDownloadResult DownloadImageByDate(DateTime date, bool forceRefresh = false)
        {
            var entry = GetEntryByDate(date, forceRefresh);
            return EnsureImageDownloaded(entry, date);
        }

        public async Task<ApodDownloadResult> DownloadImageByDateAsync(DateTime date, bool forceRefresh = false)
        {
            var entry = await GetEntryByDateAsync(date, forceRefresh).ConfigureAwait(false);
            return await EnsureImageDownloadedAsync(entry, date).ConfigureAwait(false);
        }

        public ApodApplyResult Apply(DateTime date, WallpaperStyle style, bool forceRefresh = false)
        {
            return ApplyWallpaperByDate(date, style, forceRefresh);
        }

        public ApodApplyResult ApplyWallpaperByDate(DateTime date, WallpaperStyle style, bool forceRefresh = false)
        {
            var downloadResult = DownloadImageByDate(date, forceRefresh);
            _wallpaperService.ApplyPreservingHistory(downloadResult.ImagePath, style);

            return new ApodApplyResult
            {
                Entry = downloadResult.Entry,
                ImagePath = downloadResult.ImagePath,
                DownloadedNow = downloadResult.DownloadedNow,
                Source = downloadResult.Source,
            };
        }

        public async Task<ApodApplyResult> ApplyWallpaperByDateAsync(DateTime date, WallpaperStyle style, bool forceRefresh = false)
        {
            var downloadResult = await DownloadImageByDateAsync(date, forceRefresh).ConfigureAwait(false);
            _wallpaperService.ApplyPreservingHistory(downloadResult.ImagePath, style);

            return new ApodApplyResult
            {
                Entry = downloadResult.Entry,
                ImagePath = downloadResult.ImagePath,
                DownloadedNow = downloadResult.DownloadedNow,
                Source = downloadResult.Source,
            };
        }

        public ApodApplyResult ApplyLatestAvailable(WallpaperStyle style, bool forceRefresh = false)
        {
            return ApplyLatestPublishedWallpaper(style, forceRefresh);
        }

        public ApodApplyResult ApplyLatestPublishedWallpaper(WallpaperStyle style, bool forceRefresh = false)
        {
            var latestEntry = GetLatestAvailableImageEntry(forceRefresh);
            var entryDate = DateTime.Parse(latestEntry.Date).Date;
            var downloadResult = EnsureImageDownloaded(latestEntry, entryDate);
            _wallpaperService.ApplyPreservingHistory(downloadResult.ImagePath, style);

            return new ApodApplyResult
            {
                Entry = downloadResult.Entry,
                ImagePath = downloadResult.ImagePath,
                DownloadedNow = downloadResult.DownloadedNow,
                Source = downloadResult.Source,
            };
        }

        public async Task<ApodApplyResult> ApplyLatestPublishedWallpaperAsync(WallpaperStyle style, bool forceRefresh = false)
        {
            var latestEntry = await GetLatestAvailableImageEntryAsync(forceRefresh).ConfigureAwait(false);
            var entryDate = DateTime.Parse(latestEntry.Date).Date;
            var downloadResult = await EnsureImageDownloadedAsync(latestEntry, entryDate).ConfigureAwait(false);
            _wallpaperService.ApplyPreservingHistory(downloadResult.ImagePath, style);

            return new ApodApplyResult
            {
                Entry = downloadResult.Entry,
                ImagePath = downloadResult.ImagePath,
                DownloadedNow = downloadResult.DownloadedNow,
                Source = downloadResult.Source,
            };
        }

        public string OpenPost(DateTime date)
        {
            return GetPostUrl(date);
        }

        public string GetPostUrl(DateTime date)
        {
            return ApodPageUrl.GetUrl(date);
        }

        public ApodDownloadResult EnsureImageDownloaded(ApodEntry entry, DateTime date)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (!entry.HasImage || string.IsNullOrWhiteSpace(entry.BestImageUrl))
                throw new InvalidOperationException("The selected APOD entry does not contain a downloadable image.");

            FileStorage.EnsureImagesDirectory();

            var baseName = ApodPageUrl.GetBaseName(date);
            var existingImagePath = FileStorage.TryFindExistingImagePath(baseName);
            if (LocalImageValidator.IsUsableImageFile(existingImagePath))
            {
                _cache.Upsert(entry);
                _cache.SaveLocalImagePath(date, existingImagePath);

                return new ApodDownloadResult
                {
                    Entry = entry,
                    ImagePath = existingImagePath,
                    DownloadedNow = false,
                    Source = ApodDataSource.LocalFile,
                };
            }
            else if (!string.IsNullOrWhiteSpace(existingImagePath))
            {
                AppLogger.Warn("Ignoring invalid cached APOD image at " + existingImagePath + ".");
            }

            var image = new DownloadedImageFile(entry.BestImageUrl, baseName);
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
                Source = ResolveDataSource(entry.ResolvedFromSource),
            };
        }

        public async Task<ApodDownloadResult> EnsureImageDownloadedAsync(ApodEntry entry, DateTime date)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (!entry.HasImage || string.IsNullOrWhiteSpace(entry.BestImageUrl))
                throw new InvalidOperationException("The selected APOD entry does not contain a downloadable image.");

            FileStorage.EnsureImagesDirectory();

            var baseName = ApodPageUrl.GetBaseName(date);
            var existingImagePath = FileStorage.TryFindExistingImagePath(baseName);
            if (LocalImageValidator.IsUsableImageFile(existingImagePath))
            {
                _cache.Upsert(entry);
                _cache.SaveLocalImagePath(date, existingImagePath);

                return new ApodDownloadResult
                {
                    Entry = entry,
                    ImagePath = existingImagePath,
                    DownloadedNow = false,
                    Source = ApodDataSource.LocalFile,
                };
            }
            else if (!string.IsNullOrWhiteSpace(existingImagePath))
            {
                AppLogger.Warn("Ignoring invalid cached APOD image at " + existingImagePath + ".");
            }

            var image = new DownloadedImageFile(entry.BestImageUrl, baseName);
            var downloadedNow = false;
            if (!File.Exists(image.FullPath))
            {
                await image.DownloadImageAsync().ConfigureAwait(false);
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
                Source = ResolveDataSource(entry.ResolvedFromSource),
            };
        }

        public string GetCachedLocalImagePath(DateTime date)
        {
            return TryGetLocalImagePath(date);
        }

        public void RefreshLocalImageIndex()
        {
            _cache.SyncLocalImagePaths();
        }

        public Task RefreshLocalImageIndexAsync()
        {
            return Task.Run((Action)RefreshLocalImageIndex);
        }

        public bool HasUsableLocalImage(DateTime date)
        {
            return !string.IsNullOrWhiteSpace(TryGetLocalImagePath(date));
        }

        private string TryGetLocalImagePath(DateTime date)
        {
            var baseName = ApodPageUrl.GetBaseName(date);
            var expectedPath = FileStorage.TryFindExistingImagePath(baseName);
            if (LocalImageValidator.IsUsableImageFile(expectedPath))
            {
                _cache.SaveLocalImagePath(date, expectedPath);
                return expectedPath;
            }
            else if (!string.IsNullOrWhiteSpace(expectedPath))
            {
                AppLogger.Warn("Ignoring invalid APOD image candidate at " + expectedPath + ".");
            }

            var cached = _cache.Get(date);
            if (cached != null && LocalImageValidator.IsUsableImageFile(cached.LocalImagePath))
                return cached.LocalImagePath;
            if (cached != null && !string.IsNullOrWhiteSpace(cached.LocalImagePath))
                AppLogger.Warn("Ignoring invalid cached APOD image path at " + cached.LocalImagePath + ".");

            return null;
        }

        private static ApodEntry CreateLocalFileEntry(DateTime date)
        {
            return new ApodEntry
            {
                Date = date.ToString("yyyy-MM-dd"),
                MediaType = "image",
                ResolvedFromSource = "local_file",
                IsFallbackImage = false,
            };
        }

        public DateTime GetLatestAvailableDate()
        {
            try
            {
                var latestEntry = GetLatestAvailableImageEntry();
                return DateTime.Parse(latestEntry.Date).Date;
            }
            catch
            {
                return GetLatestPublishedDate();
            }
        }

        public DateTime GetLatestPublishedDate()
        {
            try
            {
                var latestEntry = GetLatestPublishedEntry();
                return DateTime.Parse(latestEntry.Date).Date;
            }
            catch
            {
                return DateTime.UtcNow.Date;
            }
        }

        public async Task<DateTime> GetLatestPublishedDateAsync()
        {
            try
            {
                var latestEntry = await GetLatestPublishedEntryAsync().ConfigureAwait(false);
                return DateTime.Parse(latestEntry.Date).Date;
            }
            catch
            {
                return DateTime.UtcNow.Date;
            }
        }

        public async Task<DateTime> GetLatestAvailableDateAsync()
        {
            try
            {
                var latestEntry = await GetLatestAvailableImageEntryAsync().ConfigureAwait(false);
                return DateTime.Parse(latestEntry.Date).Date;
            }
            catch
            {
                return await GetLatestPublishedDateAsync().ConfigureAwait(false);
            }
        }

        public Task<ApiKeyValidationState> ValidateApiKeyAsync(string apiKey)
        {
            return _client.ValidateApiKeyAsync(apiKey);
        }

        private ApodEntry GetLatestAvailableImageEntry(bool forceRefresh = false)
        {
            var latestPublishedDate = GetLatestPublishedDate();
            for (var offset = 0; offset <= LatestImageLookback.TotalDays; offset++)
            {
                var candidateDate = latestPublishedDate.AddDays(-offset);
                var entry = GetEntryByDate(candidateDate, forceRefresh);
                if (entry != null && entry.HasImage)
                    return entry;
            }

            throw new InvalidOperationException("Unable to resolve a recent APOD entry with a downloadable image.");
        }

        private async Task<ApodEntry> GetLatestAvailableImageEntryAsync(bool forceRefresh = false)
        {
            var latestPublishedDate = await GetLatestPublishedDateAsync().ConfigureAwait(false);
            for (var offset = 0; offset <= LatestImageLookback.TotalDays; offset++)
            {
                var candidateDate = latestPublishedDate.AddDays(-offset);
                var entry = await GetEntryByDateAsync(candidateDate, forceRefresh).ConfigureAwait(false);
                if (entry != null && entry.HasImage)
                    return entry;
            }

            throw new InvalidOperationException("Unable to resolve a recent APOD entry with a downloadable image.");
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

        private static ApodDataSource ResolveDataSource(string value)
        {
            switch (value)
            {
                case "api":
                    return ApodDataSource.Api;
                case "cache":
                    return ApodDataSource.Cache;
                case "html_fallback":
                    return ApodDataSource.HtmlFallback;
                case "local_file":
                    return ApodDataSource.LocalFile;
                default:
                    return ApodDataSource.Unknown;
            }
        }

        private static bool HasLocalImage(DateTime date)
        {
            return !string.IsNullOrWhiteSpace(FileStorage.TryFindExistingImagePath(ApodPageUrl.GetBaseName(date)));
        }

        private void RefreshMonthStatus(DateTime monthStart, DateTime fetchEnd, int cachedEntriesCount, MonthRefreshMode refreshMode)
        {
            if (refreshMode == MonthRefreshMode.Balanced && cachedEntriesCount > 0)
                return;

            var entries = _client.GetEntries(monthStart, fetchEnd);
            _cache.UpsertRange(entries);
            AppLogger.Info("Refreshed APOD month availability for " + monthStart.ToString("yyyy-MM") + " entries=" + entries.Count + " mode=" + refreshMode + ".");
        }

        private async Task RefreshMonthStatusAsync(DateTime monthStart, DateTime fetchEnd, int cachedEntriesCount, MonthRefreshMode refreshMode)
        {
            if (refreshMode == MonthRefreshMode.Balanced && cachedEntriesCount > 0)
                return;

            var entries = await _client.GetEntriesAsync(monthStart, fetchEnd).ConfigureAwait(false);
            _cache.UpsertRange(entries);
            AppLogger.Info("Refreshed APOD month availability for " + monthStart.ToString("yyyy-MM") + " entries=" + entries.Count + " mode=" + refreshMode + ".");
        }

        private IReadOnlyList<ApodDayAvailability> BuildMonthStatus(DateTime monthStart, DateTime monthEnd)
        {
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
                        IsLocalImageAvailable = HasLocalImage(date),
                        IsSelectable = entry.HasImage,
                        MediaType = entry.MediaType,
                        Source = ResolveDataSource(entry.ResolvedFromSource),
                    });
                }
                else
                {
                    result.Add(new ApodDayAvailability
                    {
                        Date = date,
                        IsKnown = false,
                        HasImage = false,
                        IsLocalImageAvailable = false,
                        IsSelectable = false,
                        MediaType = null,
                        Source = ApodDataSource.Unknown,
                    });
                }
            }

            return result;
        }
    }
}
