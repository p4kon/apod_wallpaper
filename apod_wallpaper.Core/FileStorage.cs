using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace apod_wallpaper
{
    internal static class FileStorage
    {
        private static string _sessionImagesDirectoryOverride;
        public static string ImagesDirectory
        {
            get
            {
                var customPath = ResolveImagesDirectory();
                if (!string.IsNullOrWhiteSpace(customPath))
                    return customPath;

                return Path.Combine(ApplicationDataDirectory, "images");
            }
        }

        public static string CacheDirectory
        {
            get
            {
                return Path.Combine(ApplicationDataDirectory, "cache");
            }
        }

        public static string ApplicationDataDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "apod_wallpaper");
            }
        }

        public static string LogsDirectory
        {
            get
            {
                return Path.Combine(ApplicationDataDirectory, "logs");
            }
        }

        public static string MetadataCacheFilePath
        {
            get
            {
                return Path.Combine(CacheDirectory, "apod-metadata.json");
            }
        }

        public static void EnsureImagesDirectory()
        {
            Directory.CreateDirectory(ImagesDirectory);
        }

        public static void EnsureCacheDirectory()
        {
            Directory.CreateDirectory(CacheDirectory);
        }

        public static void EnsureLogsDirectory()
        {
            Directory.CreateDirectory(LogsDirectory);
        }

        public static string GetImagePath(string fileName)
        {
            return Path.Combine(ImagesDirectory, fileName);
        }

        public static string GetImagePath(string baseName, string extension)
        {
            return Path.Combine(ImagesDirectory, baseName + NormalizeImageExtension(extension));
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

        private static string ResolveImagesDirectory()
        {
            var sessionPath = NormalizePath(_sessionImagesDirectoryOverride);
            if (!string.IsNullOrWhiteSpace(sessionPath))
                return sessionPath;

            return NormalizePath(AppRuntimeSettings.ImagesDirectoryPath);
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        }
    }
}
