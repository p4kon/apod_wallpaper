using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    internal sealed class StorageSummaryService
    {
        public Task<StorageSummary> GetStorageSummaryAsync()
        {
            return Task.Run(GetStorageSummary);
        }

        internal StorageSummary GetStorageSummary()
        {
            var paths = FileStorage.EnsureStorageLayout();
            var downloadedImages = FileStorage.GetDownloadedImageFiles();
            return new StorageSummary
            {
                Paths = paths,
                Images = BuildDirectorySummary(paths.ImagesDirectory, recursive: false),
                SmartImages = BuildDirectorySummary(paths.SmartImagesDirectory, recursive: true),
                Cache = BuildDirectorySummary(paths.CacheDirectory, recursive: true),
                Logs = BuildDirectorySummary(paths.LogsDirectory, recursive: true),
                ApplicationData = BuildDirectorySummary(paths.ApplicationDataDirectory, recursive: true),
                DownloadedImageCount = downloadedImages.Count,
                DownloadedImageSizeBytes = downloadedImages.Sum(file => SafeLength(file)),
            };
        }

        private static StorageDirectorySummary BuildDirectorySummary(string path, bool recursive)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return new StorageDirectorySummary
                {
                    Path = path ?? string.Empty,
                    Exists = false,
                };
            }

            var files = Directory.GetFiles(path, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            return new StorageDirectorySummary
            {
                Path = path,
                Exists = true,
                FileCount = files.Length,
                SizeBytes = files.Sum(file => SafeLength(file)),
            };
        }

        private static long SafeLength(string path)
        {
            try
            {
                return new FileInfo(path).Length;
            }
            catch
            {
                return 0;
            }
        }
    }
}
