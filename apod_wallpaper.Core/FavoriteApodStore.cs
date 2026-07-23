using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;

namespace apod_wallpaper
{
    internal sealed class FavoriteApodStore
    {
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;
        private readonly object _syncRoot = new object();
        private readonly string _filePathOverride;

        public FavoriteApodStore()
            : this(null)
        {
        }

        public FavoriteApodStore(string filePathOverride)
        {
            _filePathOverride = string.IsNullOrWhiteSpace(filePathOverride) ? null : filePathOverride.Trim();
        }

        public IReadOnlyList<DateTime> GetDates()
        {
            lock (_syncRoot)
            {
                return Load()
                    .Dates
                    .Select(ParseDate)
                    .Where(date => date.HasValue)
                    .Select(date => date.Value.Date)
                    .Distinct()
                    .OrderByDescending(date => date)
                    .ToList();
            }
        }

        public bool IsFavorite(DateTime date)
        {
            var normalizedDate = date.Date;
            lock (_syncRoot)
            {
                return Load()
                    .Dates
                    .Select(ParseDate)
                    .Any(value => value.HasValue && value.Value.Date == normalizedDate);
            }
        }

        public bool SetFavorite(DateTime date, bool isFavorite)
        {
            var normalizedDate = date.Date;
            lock (_syncRoot)
            {
                var state = Load();
                var dates = state
                    .Dates
                    .Select(ParseDate)
                    .Where(value => value.HasValue)
                    .Select(value => value.Value.Date)
                    .Distinct()
                    .ToList();

                var contains = dates.Contains(normalizedDate);
                if (isFavorite && !contains)
                    dates.Add(normalizedDate);
                else if (!isFavorite && contains)
                    dates.Remove(normalizedDate);
                else
                    return false;

                state.Dates = dates
                    .OrderByDescending(value => value)
                    .Select(value => value.ToString("yyyy-MM-dd", InvariantCulture))
                    .ToList();
                Save(state);
                return true;
            }
        }

        private FavoriteApodState Load()
        {
            var path = GetFilePath();
            if (!File.Exists(path))
                return new FavoriteApodState();

            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var serializer = new DataContractJsonSerializer(typeof(FavoriteApodState));
                    var state = serializer.ReadObject(stream) as FavoriteApodState;
                    if (state == null)
                        return new FavoriteApodState();

                    if (state.Dates == null)
                        state.Dates = new List<string>();
                    if (state.Version <= 0)
                        state.Version = 1;

                    return state;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Unable to read favorites.json. Falling back to empty favorites.", ex);
                return new FavoriteApodState();
            }
        }

        private void Save(FavoriteApodState state)
        {
            var path = GetFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var tempPath = path + ".tmp";
            using (var stream = File.Create(tempPath))
            {
                var serializer = new DataContractJsonSerializer(typeof(FavoriteApodState));
                serializer.WriteObject(stream, state ?? new FavoriteApodState());
            }

            if (File.Exists(path))
                File.Delete(path);

            File.Move(tempPath, path);
        }

        private string GetFilePath()
        {
            return _filePathOverride ?? Path.Combine(FileStorage.GetStoragePaths().ApplicationDataDirectory, "favorites.json");
        }

        private static DateTime? ParseDate(string value)
        {
            DateTime parsed;
            if (DateTime.TryParseExact(value, "yyyy-MM-dd", InvariantCulture, DateTimeStyles.None, out parsed))
                return parsed.Date;

            return null;
        }
    }
}
