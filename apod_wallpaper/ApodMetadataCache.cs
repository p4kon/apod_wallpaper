using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;

namespace apod_wallpaper
{
    internal sealed class ApodMetadataCache
    {
        private readonly object _syncRoot = new object();
        private Dictionary<DateTime, ApodCachedEntry> _entriesByDate;

        public ApodCachedEntry Get(DateTime date)
        {
            lock (_syncRoot)
            {
                EnsureLoaded();
                ApodCachedEntry entry;
                _entriesByDate.TryGetValue(date.Date, out entry);
                return entry;
            }
        }

        public IReadOnlyList<ApodCachedEntry> GetRange(DateTime startDate, DateTime endDate)
        {
            lock (_syncRoot)
            {
                EnsureLoaded();
                return _entriesByDate
                    .Where(pair => pair.Key >= startDate.Date && pair.Key <= endDate.Date)
                    .OrderBy(pair => pair.Key)
                    .Select(pair => pair.Value)
                    .ToList();
            }
        }

        public void Upsert(ApodEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Date))
                return;

            lock (_syncRoot)
            {
                EnsureLoaded();
                var key = DateTime.Parse(entry.Date).Date;
                ApodCachedEntry existing;
                _entriesByDate.TryGetValue(key, out existing);

                var cachedEntry = ApodCachedEntry.FromEntry(entry);
                if (existing != null)
                    cachedEntry.LocalImagePath = existing.LocalImagePath;

                _entriesByDate[key] = cachedEntry;
                Save();
            }
        }

        public void UpsertRange(IEnumerable<ApodEntry> entries)
        {
            if (entries == null)
                return;

            lock (_syncRoot)
            {
                EnsureLoaded();
                var changed = false;
                foreach (var entry in entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Date))
                        continue;

                    var key = DateTime.Parse(entry.Date).Date;
                    ApodCachedEntry existing;
                    _entriesByDate.TryGetValue(key, out existing);

                    var cachedEntry = ApodCachedEntry.FromEntry(entry);
                    if (existing != null)
                        cachedEntry.LocalImagePath = existing.LocalImagePath;

                    _entriesByDate[key] = cachedEntry;
                    changed = true;
                }

                if (changed)
                    Save();
            }
        }

        public void SaveLocalImagePath(DateTime date, string localImagePath)
        {
            lock (_syncRoot)
            {
                EnsureLoaded();
                ApodCachedEntry entry;
                if (_entriesByDate.TryGetValue(date.Date, out entry))
                {
                    entry.LocalImagePath = localImagePath;
                    entry.CachedAtUtc = DateTime.UtcNow;
                    entry.LastVerifiedUtc = DateTime.UtcNow;
                    entry.LocalFileExistsAtLastCheck = !string.IsNullOrWhiteSpace(localImagePath) && File.Exists(localImagePath);
                    Save();
                }
            }
        }

        public void SyncLocalImagePaths()
        {
            lock (_syncRoot)
            {
                EnsureLoaded();
                var changed = false;

                foreach (var pair in _entriesByDate)
                {
                    var baseName = ApodPageUrl.GetBaseName(pair.Key);
                    var discoveredPath = FileStorage.TryFindExistingImagePath(baseName);
                    var exists = !string.IsNullOrWhiteSpace(discoveredPath);

                    if (!string.Equals(pair.Value.LocalImagePath, discoveredPath, StringComparison.OrdinalIgnoreCase))
                    {
                        pair.Value.LocalImagePath = discoveredPath;
                        changed = true;
                    }

                    if (pair.Value.LocalFileExistsAtLastCheck != exists)
                    {
                        pair.Value.LocalFileExistsAtLastCheck = exists;
                        changed = true;
                    }

                    pair.Value.LastVerifiedUtc = DateTime.UtcNow;
                }

                if (changed)
                    Save();
            }
        }

        private void EnsureLoaded()
        {
            if (_entriesByDate != null)
                return;

            _entriesByDate = new Dictionary<DateTime, ApodCachedEntry>();
            if (!File.Exists(FileStorage.MetadataCacheFilePath))
                return;

            try
            {
                using (var stream = File.OpenRead(FileStorage.MetadataCacheFilePath))
                {
                    var serializer = new DataContractJsonSerializer(typeof(List<ApodCachedEntry>));
                    var items = serializer.ReadObject(stream) as List<ApodCachedEntry>;
                    if (items == null)
                        return;

                    foreach (var item in items.Where(item => item != null && !string.IsNullOrWhiteSpace(item.Date)))
                    {
                        _entriesByDate[DateTime.Parse(item.Date).Date] = item;
                    }
                }
            }
            catch
            {
                _entriesByDate = new Dictionary<DateTime, ApodCachedEntry>();
            }
        }

        private void Save()
        {
            FileStorage.EnsureCacheDirectory();
            using (var stream = File.Create(FileStorage.MetadataCacheFilePath))
            {
                var serializer = new DataContractJsonSerializer(typeof(List<ApodCachedEntry>));
                serializer.WriteObject(stream, _entriesByDate.Values.OrderBy(item => item.Date).ToList());
            }
        }
    }
}
