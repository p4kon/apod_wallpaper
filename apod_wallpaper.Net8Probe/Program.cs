using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace apod_wallpaper.Net8Probe
{
    internal static class Program
    {
        private static int _failures;

        private static int Main()
        {
            var root = Path.Combine(Path.GetTempPath(), "apod_wallpaper_net8_probe_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                Run("ApplicationController creates and initializes", () => ApplicationControllerCreates(root));
                Run("DpapiUserSecretStore round-trips secret", () => DpapiRoundTrips(root));
                Run("DisplayMetrics returns valid screen bounds", DisplayMetricsReturnsScreenBounds);
                Run("SmartWallpaperComposer prepares smart image", () => SmartWallpaperComposerPreparesImage(root));
                Run("WallpaperNative registry/system call path executes", WallpaperNativeExecutes);
                Run("Store storage mode uses sandbox layout", () => StoreStorageModeUsesSandboxLayout(root));
                Run("Network HttpWebRequest fallback fetches NASA API", NetworkWebRequestFetchesApi);
                Run("Network HttpWebRequest fallback fetches APOD HTML", NetworkWebRequestFetchesHtml);
                Run("ApodPageImageExtractor parses fetched HTML", ApodPageExtractorParsesFetchedHtml);

                Console.WriteLine(_failures == 0
                    ? "Net8 probe passed."
                    : "Net8 probe failed: " + _failures);

                return _failures == 0 ? 0 : 1;
            }
            finally
            {
                TryDeleteDirectory(root);
            }
        }

        private static void ApplicationControllerCreates(string root)
        {
            var settingsPath = Path.Combine(root, "settings.json");
            var imagesPath = Path.Combine(root, "images");
            var secretPath = Path.Combine(root, "secrets");

            var controller = new apod_wallpaper.ApplicationController(
                new apod_wallpaper.JsonSettingsStore(settingsPath),
                new apod_wallpaper.DpapiUserSecretStore(secretPath),
                new apod_wallpaper.NoOpStartupRegistrationService());

            var saveResult = controller.SaveSettingsAsync(new apod_wallpaper.ApplicationSettingsSnapshot
            {
                TrayDoubleClickAction = false,
                WallpaperStyleIndex = (int)apod_wallpaper.WallpaperStyle.Smart,
                AutoRefreshEnabled = false,
                StartWithWindows = false,
                ImagesDirectoryPath = imagesPath,
                NasaApiKey = "DEMO_KEY",
                NasaApiKeyValidationState = apod_wallpaper.ApiKeyValidationState.Unknown.ToString(),
                LastAutoRefreshRunDate = string.Empty,
                LastAutoRefreshAppliedDate = string.Empty,
            }).GetAwaiter().GetResult();

            Assert(saveResult.Succeeded, "Expected settings save to succeed.");

            var initialization = controller.InitializeAsync().GetAwaiter().GetResult();
            Assert(initialization.Succeeded, "Expected initialization to succeed.");
            Assert(initialization.Value != null, "Expected initialization snapshot.");
        }

        private static void DpapiRoundTrips(string root)
        {
            var secretPath = Path.Combine(root, "secrets-dpapi");
            var store = new apod_wallpaper.DpapiUserSecretStore(secretPath);

            store.SaveNasaApiKey("probe-key");
            Assert(store.GetNasaApiKey() == "probe-key", "Expected DPAPI secret round-trip.");
            store.DeleteNasaApiKey();
            Assert(string.IsNullOrWhiteSpace(store.GetNasaApiKey()), "Expected DPAPI secret deletion.");
        }

        private static void DisplayMetricsReturnsScreenBounds()
        {
            var displayType = GetCoreType("apod_wallpaper.DisplayMetrics");
            var method = displayType.GetMethod("GetPrimaryScreenBounds", BindingFlags.Public | BindingFlags.Static);
            var rectangle = method.Invoke(null, null);
            var width = (int)rectangle.GetType().GetProperty("Width").GetValue(rectangle, null);
            var height = (int)rectangle.GetType().GetProperty("Height").GetValue(rectangle, null);

            Assert(width > 0, "Expected positive display width.");
            Assert(height > 0, "Expected positive display height.");
        }

        private static void SmartWallpaperComposerPreparesImage(string root)
        {
            var settingsPath = Path.Combine(root, "smart-settings.json");
            var imagesPath = Path.Combine(root, "smart-images");
            var secretPath = Path.Combine(root, "smart-secrets");
            Directory.CreateDirectory(imagesPath);

            var controller = new apod_wallpaper.ApplicationController(
                new apod_wallpaper.JsonSettingsStore(settingsPath),
                new apod_wallpaper.DpapiUserSecretStore(secretPath),
                new apod_wallpaper.NoOpStartupRegistrationService());

            var saveResult = controller.SaveSettingsAsync(new apod_wallpaper.ApplicationSettingsSnapshot
            {
                TrayDoubleClickAction = false,
                WallpaperStyleIndex = (int)apod_wallpaper.WallpaperStyle.Smart,
                AutoRefreshEnabled = false,
                StartWithWindows = false,
                ImagesDirectoryPath = imagesPath,
                NasaApiKey = "DEMO_KEY",
                NasaApiKeyValidationState = apod_wallpaper.ApiKeyValidationState.Unknown.ToString(),
                LastAutoRefreshRunDate = string.Empty,
                LastAutoRefreshAppliedDate = string.Empty,
            }).GetAwaiter().GetResult();
            Assert(saveResult.Succeeded, "Expected smart settings save to succeed.");

            var sourcePath = Path.Combine(root, "source.png");
            File.WriteAllBytes(sourcePath, SmallPngBytes);

            var composerType = GetCoreType("apod_wallpaper.SmartWallpaperComposer");
            var prepareMethod = composerType.GetMethod("Prepare", BindingFlags.Public | BindingFlags.Static);
            var composition = prepareMethod.Invoke(null, new object[] { sourcePath });
            var imagePath = (string)composition.GetType().GetProperty("ImagePath").GetValue(composition, null);
            var style = composition.GetType().GetProperty("Style").GetValue(composition, null);

            Assert(!string.IsNullOrWhiteSpace(imagePath), "Expected smart image path.");
            Assert(File.Exists(imagePath), "Expected composed smart image file.");
            Assert(style is apod_wallpaper.WallpaperStyle, "Expected wallpaper style result.");
        }

        private static void WallpaperNativeExecutes()
        {
            var wallpaperType = GetCoreType("apod_wallpaper.WallpaperNative");
            var getConfigMethod = wallpaperType.GetMethod("GetWallpaperConfig", BindingFlags.NonPublic | BindingFlags.Static);
            var changeWallpaperMethod = wallpaperType.GetMethod("ChangeWallpaper", BindingFlags.NonPublic | BindingFlags.Static);
            var config = getConfigMethod.Invoke(null, null);
            Assert(config != null, "Expected wallpaper configuration from registry.");

            using (var desktopKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", false))
            {
                var wallpaperPath = desktopKey != null ? desktopKey.GetValue("WallPaper") as string : null;
                Assert(!string.IsNullOrWhiteSpace(wallpaperPath), "Expected current wallpaper path from registry.");
                Assert(File.Exists(wallpaperPath), "Expected current wallpaper file to exist.");
                changeWallpaperMethod.Invoke(null, new object[] { wallpaperPath });
            }
        }

        private static void StoreStorageModeUsesSandboxLayout(string root)
        {
            var sandboxPath = Path.Combine(root, "store-sandbox");
            Directory.CreateDirectory(sandboxPath);

            apod_wallpaper.ApplicationStorageLayout.Configure(apod_wallpaper.ApplicationStorageMode.Store, sandboxPath);
            try
            {
                var controller = new apod_wallpaper.ApplicationController(
                    new apod_wallpaper.JsonSettingsStore(Path.Combine(root, "store-mode-settings.json")),
                    new apod_wallpaper.DpapiUserSecretStore(Path.Combine(root, "store-mode-secrets")),
                    new apod_wallpaper.NoOpStartupRegistrationService());
                var resetImagesDirectory = controller.UpdateSessionImagesDirectoryAsync(null).GetAwaiter().GetResult();
                Assert(resetImagesDirectory.Succeeded, "Expected session images directory reset in store storage mode.");
                var resetSettings = controller.SaveSettingsAsync(new apod_wallpaper.ApplicationSettingsSnapshot
                {
                    WallpaperStyleIndex = (int)apod_wallpaper.WallpaperStyle.Smart,
                    StartWithWindows = true,
                    AutoRefreshEnabled = false,
                    ImagesDirectoryPath = string.Empty,
                    NasaApiKeyValidationState = apod_wallpaper.ApiKeyValidationState.Unknown.ToString(),
                }).GetAwaiter().GetResult();
                Assert(resetSettings.Succeeded, "Expected runtime image directory reset in store storage mode.");

                var layout = apod_wallpaper.ApplicationStorageLayout.EnsureStorageLayout();
                Assert(layout.Mode == apod_wallpaper.ApplicationStorageMode.Store, "Expected store storage mode.");
                Assert(string.Equals(layout.ApplicationDataDirectory, sandboxPath, StringComparison.OrdinalIgnoreCase), "Expected store application data path to use provided sandbox path.");
                Assert(string.Equals(layout.ImagesDirectory, Path.Combine(sandboxPath, "images"), StringComparison.OrdinalIgnoreCase), "Expected store images directory inside sandbox path.");

                var settingsStore = new apod_wallpaper.JsonSettingsStore();
                var secretStore = new apod_wallpaper.DpapiUserSecretStore();
                settingsStore.Save(new apod_wallpaper.ApplicationSettingsSnapshot
                {
                    WallpaperStyleIndex = (int)apod_wallpaper.WallpaperStyle.Smart,
                    StartWithWindows = true,
                    AutoRefreshEnabled = false,
                    NasaApiKeyValidationState = apod_wallpaper.ApiKeyValidationState.Unknown.ToString(),
                });
                secretStore.SaveNasaApiKey("sandbox-probe-key");

                Assert(File.Exists(Path.Combine(sandboxPath, "settings.json")), "Expected settings.json in store sandbox path.");
                Assert(File.Exists(Path.Combine(sandboxPath, "secrets", "nasa-api-key.bin")), "Expected protected secret file in store sandbox path.");
                Assert(secretStore.GetNasaApiKey() == "sandbox-probe-key", "Expected DPAPI secret round-trip in store storage mode.");
            }
            finally
            {
                apod_wallpaper.ApplicationStorageLayout.ResetConfiguration();
            }
        }

        private static void NetworkWebRequestFetchesApi()
        {
            var networkType = GetCoreType("apod_wallpaper.Network");
            var method = networkType.GetMethod("DownloadStringWithWebRequest", BindingFlags.NonPublic | BindingFlags.Static);
            var apiKey = new apod_wallpaper.DpapiUserSecretStore().GetNasaApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
                apiKey = "DEMO_KEY";

            try
            {
                var result = (string)method.Invoke(null, new object[] { "https://api.nasa.gov/planetary/apod?api_key=" + apiKey + "&date=2026-04-07" });
                if (string.IsNullOrWhiteSpace(result))
                    return;

                Assert(result.IndexOf("\"date\"", StringComparison.OrdinalIgnoreCase) >= 0, "Expected NASA API JSON payload.");
            }
            catch (TargetInvocationException ex) when (ex.InnerException is System.Net.WebException webException)
            {
                var response = webException.Response as System.Net.HttpWebResponse;
                Assert(response != null, "Expected NASA API webrequest path to reach the remote server.");
                Assert((int)response.StatusCode >= 400, "Expected a real HTTP response when NASA API rejects the request.");
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null &&
                                                       ex.InnerException.GetType().FullName == "apod_wallpaper.Network+NetworkHttpStatusException")
            {
                var statusCodeProperty = ex.InnerException.GetType().GetProperty("StatusCode");
                var statusCode = statusCodeProperty != null
                    ? (int)statusCodeProperty.GetValue(ex.InnerException, null)
                    : 0;
                Assert(statusCode >= 400, "Expected a real HTTP status when NASA API rejects the request.");
            }
        }

        private static void NetworkWebRequestFetchesHtml()
        {
            var networkType = GetCoreType("apod_wallpaper.Network");
            var method = networkType.GetMethod("DownloadStringWithWebRequest", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (string)method.Invoke(null, new object[] { "https://apod.nasa.gov/apod/ap260408.html" });
            Assert(!string.IsNullOrWhiteSpace(result), "Expected APOD HTML response body.");
            Assert(result.IndexOf("earthset_original.jpg", StringComparison.OrdinalIgnoreCase) >= 0, "Expected APOD HTML image payload.");
        }

        private static void ApodPageExtractorParsesFetchedHtml()
        {
            var networkType = GetCoreType("apod_wallpaper.Network");
            var webRequestMethod = networkType.GetMethod("DownloadStringWithWebRequest", BindingFlags.NonPublic | BindingFlags.Static);
            var html = (string)webRequestMethod.Invoke(null, new object[] { "https://apod.nasa.gov/apod/ap260408.html" });

            var extractorType = GetCoreType("apod_wallpaper.ApodPageImageExtractor");
            var extractMethod = extractorType.GetMethod("TryExtract", BindingFlags.Public | BindingFlags.Static);
            var parameters = new object[]
            {
                html,
                "https://apod.nasa.gov/apod/ap260408.html",
                null,
                null,
            };
            var extracted = (bool)extractMethod.Invoke(null, parameters);
            var previewUrl = parameters[2] as string;
            var imageUrl = parameters[3] as string;

            Assert(extracted, "Expected HTML extractor to parse fetched APOD HTML.");
            Assert(previewUrl.IndexOf(".jpg", StringComparison.OrdinalIgnoreCase) >= 0, "Expected preview URL from HTML extractor.");
            Assert(imageUrl.IndexOf(".jpg", StringComparison.OrdinalIgnoreCase) >= 0, "Expected image URL from HTML extractor.");
        }

        private static Type GetCoreType(string fullName)
        {
            return typeof(apod_wallpaper.ApplicationController).Assembly.GetType(fullName, throwOnError: true);
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine("[PASS] " + name);
            }
            catch (Exception ex)
            {
                _failures++;
                Console.WriteLine("[FAIL] " + name + ": " + ex.Message);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch
            {
            }
        }

        private static readonly byte[] SmallPngBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO7Z0mQAAAAASUVORK5CYII=");
    }
}
