using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace apod_wallpaper
{
    internal static class FileStorage
    {
        private const string StorageModeEnvironmentVariable = "APOD_WALLPAPER_STORAGE_MODE";
        private const string PortableRootEnvironmentVariable = "APOD_WALLPAPER_PORTABLE_ROOT";
        private const string PortableMarkerFileName = "portable.mode";

        private static string _sessionImagesDirectoryOverride;
        private static ApplicationStorageMode? _modeOverride;
        private static string _applicationDataDirectoryOverride;

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
            var executableImagesDirectory = Path.Combine(GetDefaultPortableRootDirectory(), "images");
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
                SettingsFilePath = Path.Combine(applicationDataDirectory, "settings.json"),
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

        public static IReadOnlyList<string> GetDownloadedImageFiles()
        {
            var imagesDirectory = ImagesDirectory;
            if (string.IsNullOrWhiteSpace(imagesDirectory) || !Directory.Exists(imagesDirectory))
                return Array.Empty<string>();

            return Directory
                .GetFiles(imagesDirectory)
                .Where(path => IsSupportedImageExtension(NormalizeImageExtension(Path.GetExtension(path))))
                .Where(LocalImageValidator.IsUsableImageFile)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static IReadOnlyList<DateTime> GetDownloadedImageDates()
        {
            var dates = new HashSet<DateTime>();
            foreach (var path in GetDownloadedImageFiles())
            {
                DateTime parsedDate;
                if (DateTime.TryParseExact(
                    Path.GetFileNameWithoutExtension(path),
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out parsedDate))
                {
                    dates.Add(parsedDate.Date);
                }
            }

            return dates.OrderBy(date => date).ToList();
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

        private static bool IsSupportedImageExtension(string extension)
        {
            return extension == ".jpg" ||
                extension == ".jpeg" ||
                extension == ".png" ||
                extension == ".bmp" ||
                extension == ".gif" ||
                extension == ".webp" ||
                extension == ".tif" ||
                extension == ".tiff";
        }

        public static string NormalizeImageExtension(string extension)
        {
            return ImageFormatCatalog.NormalizeImageExtension(extension);
        }

        public static void SetSessionImagesDirectory(string path)
        {
            _sessionImagesDirectoryOverride = NormalizePath(path);
        }

        internal static void Configure(ApplicationStorageMode? mode, string applicationDataDirectoryOverride)
        {
            _modeOverride = mode;
            _applicationDataDirectoryOverride = NormalizePath(applicationDataDirectoryOverride);
        }

        internal static void SetStorageModeOverride(ApplicationStorageMode? mode)
        {
            Configure(mode, _applicationDataDirectoryOverride);
        }

        internal static void SetApplicationDataDirectoryOverride(string path)
        {
            Configure(_modeOverride, path);
        }

        public static string GetDefaultPortableRootDirectory()
        {
            var environmentRoot = NormalizePath(Environment.GetEnvironmentVariable(PortableRootEnvironmentVariable));
            if (!string.IsNullOrWhiteSpace(environmentRoot))
                return environmentRoot;

            var baseDirectory = NormalizePath(AppDomain.CurrentDomain.BaseDirectory);
            if (IsPortableRoot(baseDirectory))
                return baseDirectory;

            if (!string.IsNullOrWhiteSpace(baseDirectory))
            {
                var directoryName = Path.GetFileName(baseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                var parentDirectory = Directory.GetParent(baseDirectory)?.FullName;
                if (string.Equals(directoryName, "app", StringComparison.OrdinalIgnoreCase) && IsPortableRoot(parentDirectory))
                    return NormalizePath(parentDirectory);
            }

            return baseDirectory;
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

            if (mode == ApplicationStorageMode.Store)
                return Path.Combine(applicationDataDirectory, "images");

            if (CanUseDirectory(executableImagesDirectory))
                return executableImagesDirectory;

            return Path.Combine(applicationDataDirectory, "images");
        }

        private static ApplicationStorageMode ResolveStorageMode()
        {
            if (_modeOverride.HasValue)
                return _modeOverride.Value;

            var environmentOverride = NormalizePath(Environment.GetEnvironmentVariable(StorageModeEnvironmentVariable));
            if (string.Equals(environmentOverride, "portable", System.StringComparison.OrdinalIgnoreCase))
                return ApplicationStorageMode.Portable;

            if (string.Equals(environmentOverride, "localappdata", System.StringComparison.OrdinalIgnoreCase))
                return ApplicationStorageMode.LocalApplicationData;

            if (string.Equals(environmentOverride, "store", System.StringComparison.OrdinalIgnoreCase))
                return ApplicationStorageMode.Store;

            if (IsPortableRoot(GetDefaultPortableRootDirectory()))
                return ApplicationStorageMode.Portable;

            return ApplicationStorageMode.LocalApplicationData;
        }

        private static string ResolveApplicationDataDirectory(ApplicationStorageMode mode)
        {
            var overridePath = NormalizePath(_applicationDataDirectoryOverride);
            if (!string.IsNullOrWhiteSpace(overridePath))
                return overridePath;

            if (mode == ApplicationStorageMode.Portable)
                return Path.Combine(GetDefaultPortableRootDirectory(), "data");

            if (mode == ApplicationStorageMode.Store)
                throw new InvalidOperationException("Store storage mode requires a host-provided application data directory.");

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

        private static bool IsPortableRoot(string directory)
        {
            return !string.IsNullOrWhiteSpace(directory) &&
                File.Exists(Path.Combine(directory, PortableMarkerFileName));
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
