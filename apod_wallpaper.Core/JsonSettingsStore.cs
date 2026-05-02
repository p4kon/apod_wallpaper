using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Json;

namespace apod_wallpaper
{
    public sealed class JsonSettingsStore : IApplicationSettingsStore
    {
        private readonly string _settingsFilePathOverride;

        public JsonSettingsStore()
            : this(null)
        {
        }

        public JsonSettingsStore(string settingsFilePathOverride)
        {
            _settingsFilePathOverride = string.IsNullOrWhiteSpace(settingsFilePathOverride) ? null : settingsFilePathOverride.Trim();
        }

        public bool Exists()
        {
            return File.Exists(GetSettingsFilePath());
        }

        public ApplicationSettingsSnapshot Load()
        {
            var path = GetSettingsFilePath();
            if (!File.Exists(path))
                return CreateDefaultSnapshot();

            try
            {
                var json = File.ReadAllText(path);
                using (var stream = File.OpenRead(path))
                {
                    var serializer = new DataContractJsonSerializer(typeof(ApplicationSettingsSnapshot));
                    var snapshot = serializer.ReadObject(stream) as ApplicationSettingsSnapshot;
                    if (snapshot == null)
                        return CreateDefaultSnapshot();

                    if (json.IndexOf("MinimizeToTrayOnClose", StringComparison.OrdinalIgnoreCase) < 0)
                        snapshot.MinimizeToTrayOnClose = true;

                    return snapshot;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Unable to read settings.json. Falling back to defaults. " + ex.Message);
                return CreateDefaultSnapshot();
            }
        }

        public void Save(ApplicationSettingsSnapshot settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var path = GetSettingsFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var tempPath = path + ".tmp";
            using (var stream = File.Create(tempPath))
            {
                var serializer = new DataContractJsonSerializer(typeof(ApplicationSettingsSnapshot));
                serializer.WriteObject(stream, settings);
            }

            if (File.Exists(path))
                File.Delete(path);

            File.Move(tempPath, path);
        }

        internal static ApplicationSettingsSnapshot CreateDefaultSnapshot()
        {
            return new ApplicationSettingsSnapshot
            {
                TrayDoubleClickAction = false,
                WallpaperStyleIndex = (int)WallpaperStyle.Smart,
                AutoRefreshEnabled = false,
                StartWithWindows = true,
                MinimizeToTrayOnClose = true,
                NasaApiKeyValidationState = ApiKeyValidationState.Unknown.ToString(),
                ImagesDirectoryPath = string.Empty,
                LastAutoRefreshRunDate = string.Empty,
                LastAutoRefreshAppliedDate = string.Empty,
            };
        }

        private string GetSettingsFilePath()
        {
            return _settingsFilePathOverride ?? ApplicationStorageLayout.GetStoragePaths().SettingsFilePath;
        }
    }
}
