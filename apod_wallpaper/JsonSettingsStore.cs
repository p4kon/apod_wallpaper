using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Json;

namespace apod_wallpaper
{
    internal sealed class JsonSettingsStore : IApplicationSettingsStore
    {
        private readonly LegacyPropertiesSettingsBridge _legacyBridge;
        private readonly string _settingsFilePathOverride;

        public JsonSettingsStore()
            : this(null, new LegacyPropertiesSettingsBridge())
        {
        }

        internal JsonSettingsStore(string settingsFilePathOverride, LegacyPropertiesSettingsBridge legacyBridge)
        {
            _settingsFilePathOverride = string.IsNullOrWhiteSpace(settingsFilePathOverride) ? null : settingsFilePathOverride.Trim();
            _legacyBridge = legacyBridge ?? throw new ArgumentNullException(nameof(legacyBridge));
        }

        public ApplicationSettingsSnapshot Load()
        {
            var path = GetSettingsFilePath();
            if (!File.Exists(path))
                return CreateDefaultSnapshot();

            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var serializer = new DataContractJsonSerializer(typeof(ApplicationSettingsSnapshot));
                    var snapshot = serializer.ReadObject(stream) as ApplicationSettingsSnapshot;
                    return snapshot ?? CreateDefaultSnapshot();
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

        public string LoadLegacyApiKey()
        {
            return _legacyBridge.LoadLegacyApiKey();
        }

        public void ClearLegacyApiKey()
        {
            _legacyBridge.ClearLegacyApiKey();
        }

        public void MigrateLegacySettingsIfNeeded()
        {
            var path = GetSettingsFilePath();
            if (File.Exists(path))
                return;

            Save(_legacyBridge.LoadLegacySettings());
            _legacyBridge.ClearLegacySettingsPreservingApiKey();
        }

        private string GetSettingsFilePath()
        {
            return _settingsFilePathOverride ?? ApplicationStorageLayout.GetStoragePaths().SettingsFilePath;
        }

        private static ApplicationSettingsSnapshot CreateDefaultSnapshot()
        {
            return new ApplicationSettingsSnapshot
            {
                TrayDoubleClickAction = false,
                WallpaperStyleIndex = (int)WallpaperStyle.Smart,
                AutoRefreshEnabled = false,
                StartWithWindows = true,
                NasaApiKeyValidationState = ApiKeyValidationState.Unknown.ToString(),
                ImagesDirectoryPath = string.Empty,
                LastAutoRefreshRunDate = string.Empty,
                LastAutoRefreshAppliedDate = string.Empty,
            };
        }
    }
}
