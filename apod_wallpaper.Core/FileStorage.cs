using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace apod_wallpaper
{
    internal static class FileStorage
    {
        private static string _sessionImagesDirectoryOverride;
        private static ApplicationStorageMode? _modeOverride;

        public static string ImagesDirectory
        {
            get
            {
                return GetStoragePaths().ImagesDirectory;
            }
        }

        public static string SmartImagesDirectory
        {
            get
            {
                return GetStoragePaths().SmartImagesDirectory;
            }
        }

        public static string CacheDirectory
        {
            get
            {
                return GetStoragePaths().CacheDirectory;
            }
        }

        public static string ApplicationDataDirectory
        {
            get
            {
                return GetStoragePaths().ApplicationDataDirectory;
            }
        }

        public static string LogsDirectory
        {
            get
            {
                return GetStoragePaths().LogsDirectory;
            }
        }

        public static string MetadataCacheFilePath
        {
            get
            {
                return GetStoragePaths().MetadataCacheFilePath;
            }
        }

        public static string SecretsDirectory
        {
            get
            {
                return GetStoragePaths().SecretsDirectory;
            }
        }

        public static void EnsureImagesDirectory()
        {
            Directory.CreateDirectory(ImagesDirectory);
        }

        public static void EnsureSmartImagesDirectory()
        {
            Directory.CreateDirectory(SmartImagesDirectory);
        }

        public static void EnsureCacheDirectory()
        {
            Directory.CreateDirectory(CacheDirectory);
        }

        public static void EnsureLogsDirectory()
        {
            Directory.CreateDirectory(LogsDirectory);
        }

        public static ApplicationStoragePaths GetStoragePaths()
        {
            var mode = ResolveStorageMode();
            var customImagesDirectory = ResolveImagesDirectory();
            var usesCustomImagesDirectory = !string.IsNullOrWhiteSpace(customImagesDirectory);
            var applicationDataDirectory = ResolveApplicationDataDirectory(mode);
            var executableImagesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images");
            var imagesDirectory = usesCustomImagesDirectory
                ? customImagesDirectory
                : ResolveDefaultImagesDirectory(mode, executableImagesDirectory, applicationDataDirectory);

            return new ApplicationStoragePaths
            {
                Mode = mode,
                ApplicationDataDirectory = applicationDataDirectory,
                ImagesDirectory = imagesDirectory,
                SmartImagesDirectory = Path.Combine(imagesDirectory, "smart"),
                CacheDirectory = Path.Combine(applicationDataDirectory, "cache"),
                LogsDirectory = Path.Combine(applicationDataDirectory, "logs"),
                MetadataCacheFilePath = Path.Combine(applicationDataDirectory, "cache", "apod-metadata.json"),
                SecretsDirectory = Path.Combine(applicationDataDirectory, "secrets"),
                UsesCustomImagesDirectory = usesCustomImagesDirectory,
                UsesExecutableImagesDirectory = !usesCustomImagesDirectory &&
                    PathsEqual(imagesDirectory, executableImagesDirectory),
            };
        }

        public static ApplicationStoragePaths EnsureStorageLayout()
        {
            var paths = GetStoragePaths();
            Directory.CreateDirectory(paths.ImagesDirectory);
            Directory.CreateDirectory(paths.SmartImagesDirectory);
            Directory.CreateDirectory(paths.CacheDirectory);
            Directory.CreateDirectory(paths.LogsDirectory);
            Directory.CreateDirectory(paths.SecretsDirectory);
            return paths;
        }

        public static string GetImagePath(string fileName)
        {
            return Path.Combine(ImagesDirectory, fileName);
        }

        public static string GetImagePath(string baseName, string extension)
        {
            return Path.Combine(ImagesDirectory, baseName + NormalizeImageExtension(extension));
        }

        public static string GetSmartImagePath(string fileName)
        {
            return Path.Combine(SmartImagesDirectory, fileName);
        }

        public static string TryFindExistingImagePath(string baseName)
        {
            return GetKnownImagePaths(baseName).FirstOrDefault(File.Exists);
        }

        public static IReadOnlyList<string> GetKnownImagePaths(string baseName)
        {
            return new[]
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".bmp",
                ".gif",
                ".webp",
                ".tif",
                ".tiff",
            }
                .Select(extension => Path.Combine(ImagesDirectory, baseName + extension))
                .ToList();
        }

        public static string NormalizeImageExtension(string extension)
        {
            return ImageFormatCatalog.NormalizeImageExtension(extension);
        }

        public static void SetSessionImagesDirectory(string path)
        {
            _sessionImagesDirectoryOverride = NormalizePath(path);
        }

        internal static void SetStorageModeOverride(ApplicationStorageMode? mode)
        {
            _modeOverride = mode;
        }

        private static string ResolveImagesDirectory()
        {
            var sessionPath = NormalizePath(_sessionImagesDirectoryOverride);
            if (!string.IsNullOrWhiteSpace(sessionPath))
                return sessionPath;

            return NormalizePath(AppRuntimeSettings.ImagesDirectoryPath);
        }

        private static string ResolveDefaultImagesDirectory(ApplicationStorageMode mode, string executableImagesDirectory, string applicationDataDirectory)
        {
            if (mode == ApplicationStorageMode.Portable)
                return executableImagesDirectory;

            if (CanUseDirectory(executableImagesDirectory))
                return executableImagesDirectory;

            return Path.Combine(applicationDataDirectory, "images");
        }

        private static ApplicationStorageMode ResolveStorageMode()
        {
            if (_modeOverride.HasValue)
                return _modeOverride.Value;

            var environmentOverride = NormalizePath(Environment.GetEnvironmentVariable("APOD_WALLPAPER_STORAGE_MODE"));
            if (string.Equals(environmentOverride, "portable", System.StringComparison.OrdinalIgnoreCase))
                return ApplicationStorageMode.Portable;

            if (string.Equals(environmentOverride, "localappdata", System.StringComparison.OrdinalIgnoreCase))
                return ApplicationStorageMode.LocalApplicationData;

            var portableMarkerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "portable.mode");
            if (File.Exists(portableMarkerPath))
                return ApplicationStorageMode.Portable;

            return ApplicationStorageMode.LocalApplicationData;
        }

        private static string ResolveApplicationDataDirectory(ApplicationStorageMode mode)
        {
            if (mode == ApplicationStorageMode.Portable)
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "apod_wallpaper");
        }

        private static bool CanUseDirectory(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                var probePath = Path.Combine(path, ".write-test-" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(probePath, "ok");
                File.Delete(probePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        }

        private static bool PathsEqual(string left, string right)
        {
            return string.Equals(
                NormalizePath(left),
                NormalizePath(right),
                System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
