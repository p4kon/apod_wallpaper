using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace apod_wallpaper.SmokeTests
{
    internal static class Program
    {
        private static int _failures;
        private static string _secretStoreDirectory;

        [STAThread]
        private static int Main()
        {
            _secretStoreDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_smoke_secrets_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_secretStoreDirectory);
            apod_wallpaper.UserSecretStore.SetSecretDirectoryOverride(_secretStoreDirectory);

            try
            {
                Run("Scheduler restarts after stop inside callback", SchedulerRestartsAfterStopInsideCallback);
                Run("Future date is unavailable sync", FutureDateIsUnavailableSync);
                Run("Future date is unavailable async", FutureDateIsUnavailableAsync);
                Run("API key change resets validation state", ApiKeyChangeResetsValidationState);
                Run("Preferred display date uses last applied date", PreferredDisplayDateUsesLastAppliedDate);
                Run("HTML extractor resolves preview and full image", HtmlExtractorResolvesImagePage);
                Run("HTML extractor rejects video page", HtmlExtractorRejectsVideoPage);
                Run("HTML extractor handles annotated image page", HtmlExtractorHandlesAnnotatedImagePage);
                Run("Runtime settings fall back to DEMO_KEY for invalid key", InvalidApiKeyFallsBackToDemoKey);
                Run("Local image is preferred for preview", LocalImageIsPreferredForPreview);
                Run("ApplyLatestPublished walks back through video days", ApplyLatestPublishedFallsBackAcrossVideoDays);
                Run("Smart composer stretches near screen ratio images", SmartComposerUsesStretchForNearScreenRatio);
                Run("Smart composer creates single focus image for square content", SmartComposerCreatesSingleFocusForSquareImages);
                Run("Scheduler uses hourly polling for DEMO_KEY", SchedulerUsesHourlyPollingForDemoKey);
                Run("Scheduler uses 30 minute polling for personal key", SchedulerUsesThirtyMinutePollingForPersonalKey);
                Run("Wallpaper service rejects invalid local file", WallpaperServiceRejectsInvalidLocalFile);
                Run("Scheduler day lock skips repeated checks for today", SchedulerDayLockSkipsRepeatedChecks);
                Run("Scheduler day lock does not skip when no applied date", SchedulerDayLockRequiresAppliedDate);
                Run("API key is stored outside plaintext settings", ApiKeyIsStoredOutsidePlaintextSettings);
                Run("Legacy API key migrates to protected storage", LegacyApiKeyMigratesToProtectedStorage);
                Run("Initial state snapshot returns startup data in one call", InitialStateSnapshotReturnsStartupData);
                Run("Json settings store writes non-secret settings to settings.json", JsonSettingsStoreWritesSettingsFile);
                Run("Legacy non-secret settings migrate to json store", LegacySettingsMigrateToJsonStore);
                Run("Storage layout resolves all backend paths centrally", StorageLayoutResolvesAllPathsCentrally);
                Run("Portable storage mode keeps app data near executable", PortableStorageModeUsesPortableLayout);
                Run("Public facade methods use operation results", PublicFacadeMethodsUseOperationResults);
                Run("Public workflow payload never exposes failed status", FailedWorkflowStatusMapsToOperationError);
                Run("Backend facade does not expose diagnostics contract", BackendFacadeDoesNotExposeDiagnosticsContract);
                Run("WallpaperApplied subscription disposes cleanly", WallpaperAppliedSubscriptionDisposesCleanly);

                Console.WriteLine(_failures == 0
                    ? "Smoke tests passed."
                    : "Smoke tests failed: " + _failures);

                return _failures == 0 ? 0 : 1;
            }
            finally
            {
                apod_wallpaper.UserSecretStore.ClearSecretDirectoryOverride();
                TryDeleteDirectory(_secretStoreDirectory);
            }
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
                Interlocked.Increment(ref _failures);
                Console.WriteLine("[FAIL] " + name + ": " + ex.Message);
            }
        }

        private static void SchedulerRestartsAfterStopInsideCallback()
        {
            using (var scheduler = new apod_wallpaper.Scheduler())
            {
                scheduler.PollingInterval = TimeSpan.FromMilliseconds(50);

                var firstRun = new ManualResetEventSlim(false);
                scheduler.Start(() =>
                {
                    scheduler.Stop();
                    firstRun.Set();
                });

                if (!firstRun.Wait(TimeSpan.FromSeconds(2)))
                    throw new InvalidOperationException("Scheduler did not fire the first callback.");

                var secondRun = new ManualResetEventSlim(false);
                scheduler.Start(() =>
                {
                    scheduler.Stop();
                    secondRun.Set();
                });

                if (!secondRun.Wait(TimeSpan.FromSeconds(2)))
                    throw new InvalidOperationException("Scheduler did not restart after stopping inside the callback.");
            }
        }

        private static void FutureDateIsUnavailableSync()
        {
            var workflow = new apod_wallpaper.ApodWorkflowService();
            var result = workflow.LoadDay(DateTime.Today.AddDays(1), false);
            Assert(result.Status == apod_wallpaper.ApodWorkflowStatus.Unavailable, "Expected Unavailable status for a future date.");
        }

        private static void FutureDateIsUnavailableAsync()
        {
            var workflow = new apod_wallpaper.ApodWorkflowService();
            var result = workflow.LoadDayAsync(DateTime.Today.AddDays(2), false).GetAwaiter().GetResult();
            Assert(result.Status == apod_wallpaper.ApodWorkflowStatus.Unavailable, "Expected async Unavailable status for a future date.");
        }

        private static void ApiKeyChangeResetsValidationState()
        {
            var snapshot = CaptureSettings();
            try
            {
                var controller = CreateController();
                var firstSaveResult = controller.SaveSettingsAsync(new apod_wallpaper.ApplicationSettingsSnapshot
                {
                    TrayDoubleClickAction = snapshot.TrayDoubleClickAction,
                    WallpaperStyleIndex = snapshot.WallpaperStyleIndex,
                    AutoRefreshEnabled = false,
                    StartWithWindows = snapshot.StartWithWindows,
                    ImagesDirectoryPath = snapshot.ImagesDirectoryPath,
                    NasaApiKey = "first-key",
                    NasaApiKeyValidationState = apod_wallpaper.ApiKeyValidationState.Valid.ToString(),
                    LastAutoRefreshRunDate = snapshot.LastAutoRefreshRunDate,
                    LastAutoRefreshAppliedDate = snapshot.LastAutoRefreshAppliedDate,
                }).GetAwaiter().GetResult();
                Assert(firstSaveResult.Succeeded, "Expected first settings save to succeed.");

                var secondSaveResult = controller.SaveSettingsAsync(new apod_wallpaper.ApplicationSettingsSnapshot
                {
                    TrayDoubleClickAction = snapshot.TrayDoubleClickAction,
                    WallpaperStyleIndex = snapshot.WallpaperStyleIndex,
                    AutoRefreshEnabled = false,
                    StartWithWindows = snapshot.StartWithWindows,
                    ImagesDirectoryPath = snapshot.ImagesDirectoryPath,
                    NasaApiKey = "second-key",
                    NasaApiKeyValidationState = apod_wallpaper.ApiKeyValidationState.Valid.ToString(),
                    LastAutoRefreshRunDate = snapshot.LastAutoRefreshRunDate,
                    LastAutoRefreshAppliedDate = snapshot.LastAutoRefreshAppliedDate,
                }).GetAwaiter().GetResult();
                Assert(secondSaveResult.Succeeded, "Expected second settings save to succeed.");

                Assert(GetValueOrThrow(controller.GetApiKeyValidationStateAsync().GetAwaiter().GetResult(), "Unable to read API key validation state.") == apod_wallpaper.ApiKeyValidationState.Unknown,
                    "Expected validation state to reset to Unknown after API key change.");
            }
            finally
            {
                RestoreSettings(snapshot);
            }
        }

        private static void PreferredDisplayDateUsesLastAppliedDate()
        {
            var snapshot = CaptureSettings();
            try
            {
                var controller = CreateController();
                var saveResult = controller.SaveSettingsAsync(new apod_wallpaper.ApplicationSettingsSnapshot
                {
                    TrayDoubleClickAction = snapshot.TrayDoubleClickAction,
                    WallpaperStyleIndex = snapshot.WallpaperStyleIndex,
                    AutoRefreshEnabled = snapshot.AutoRefreshEnabled,
                    StartWithWindows = snapshot.StartWithWindows,
                    ImagesDirectoryPath = snapshot.ImagesDirectoryPath,
                    NasaApiKey = snapshot.NasaApiKey,
                    NasaApiKeyValidationState = snapshot.NasaApiKeyValidationState,
                    LastAutoRefreshRunDate = snapshot.LastAutoRefreshRunDate,
                    LastAutoRefreshAppliedDate = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd"),
                }).GetAwaiter().GetResult();
                Assert(saveResult.Succeeded, "Expected settings save for preferred display date to succeed.");

                Assert(GetValueOrThrow(controller.GetPreferredDisplayDateAsync().GetAwaiter().GetResult(), "Unable to resolve preferred display date.") == DateTime.Today.AddDays(-1),
                    "Expected preferred display date to use the last auto-applied date.");
            }
            finally
            {
                RestoreSettings(snapshot);
            }
        }

        private static void HtmlExtractorResolvesImagePage()
        {
            const string html =
@"2026 April 8
<br>
<a href=""image/2604/earthset_original.jpg"">
<IMG SRC=""image/2604/earthset_700.jpg"" style=""max-width:100%""></a>
</center>";

            string previewUrl;
            string imageUrl;
            var result = apod_wallpaper.ApodPageImageExtractor.TryExtract(
                html,
                "https://apod.nasa.gov/apod/ap260408.html",
                out previewUrl,
                out imageUrl);

            Assert(result, "Expected extractor to resolve an image page.");
            Assert(previewUrl == "https://apod.nasa.gov/apod/image/2604/earthset_700.jpg", "Unexpected preview URL.");
            Assert(imageUrl == "https://apod.nasa.gov/apod/image/2604/earthset_original.jpg", "Unexpected full image URL.");
        }

        private static void HtmlExtractorRejectsVideoPage()
        {
            const string html =
@"2026 April 9
<br>
<video width=""960"" height=""540"" controls autoplay muted>
<source src=""image/2604/comet_plunge.mp4"" type=""video/mp4"">
</video>
</center>";

            string previewUrl;
            string imageUrl;
            var result = apod_wallpaper.ApodPageImageExtractor.TryExtract(
                html,
                "https://apod.nasa.gov/apod/ap260409.html",
                out previewUrl,
                out imageUrl);

            Assert(!result, "Expected extractor to reject a video page.");
            Assert(string.IsNullOrEmpty(previewUrl), "Preview URL should be empty for video pages.");
            Assert(string.IsNullOrEmpty(imageUrl), "Image URL should be empty for video pages.");
        }

        private static void HtmlExtractorHandlesAnnotatedImagePage()
        {
            const string html =
@"2026 March 15
<br>
<a href=""image/2603/MayanMilkyWay_Fernandez_1600.jpg""
onMouseOver=""if (document.images) document.imagename1.src='image/2603/MayanMilkyWay_Fernandez_1080_annotated.jpg';""
onMouseOut=""if (document.images) document.imagename1.src='image/2603/MayanMilkyWay_Fernandez_1080.jpg';"">
<IMG SRC=""image/2603/MayanMilkyWay_Fernandez_1080.jpg"" name=imagename1 style=""max-width:100%""></a>
</center>";

            string previewUrl;
            string imageUrl;
            var result = apod_wallpaper.ApodPageImageExtractor.TryExtract(
                html,
                "https://apod.nasa.gov/apod/ap260315.html",
                out previewUrl,
                out imageUrl);

            Assert(result, "Expected extractor to resolve an annotated image page.");
            Assert(previewUrl == "https://apod.nasa.gov/apod/image/2603/MayanMilkyWay_Fernandez_1080.jpg", "Unexpected preview URL for annotated image page.");
            Assert(imageUrl == "https://apod.nasa.gov/apod/image/2603/MayanMilkyWay_Fernandez_1600.jpg", "Unexpected full image URL for annotated image page.");
        }

        private static void InvalidApiKeyFallsBackToDemoKey()
        {
            apod_wallpaper.AppRuntimeSettings.Configure("invalid-key", null, apod_wallpaper.ApiKeyValidationState.Invalid);
            Assert(apod_wallpaper.AppRuntimeSettings.NasaApiKey == "DEMO_KEY", "Expected DEMO_KEY fallback for invalid API key.");
            Assert(apod_wallpaper.AppRuntimeSettings.RawNasaApiKey == "invalid-key", "Expected raw API key to remain available.");
        }

        private static void LocalImageIsPreferredForPreview()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_smoke_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);

            var snapshot = CaptureSettings();
            try
            {
                var date = new DateTime(2026, 4, 18);
                var imagePath = Path.Combine(tempDirectory, "2026-04-18.jpg");
                using (var bitmap = new Bitmap(8, 8))
                {
                    bitmap.Save(imagePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                apod_wallpaper.FileStorage.SetSessionImagesDirectory(tempDirectory);
                var workflow = new apod_wallpaper.ApodWorkflowService();
                var result = workflow.LoadDay(date, false);

                Assert(result.Status == apod_wallpaper.ApodWorkflowStatus.Success, "Expected local preview workflow to succeed.");
                Assert(result.IsLocalFile, "Expected local file to be used for preview.");
                Assert(string.Equals(result.PreviewLocation, imagePath, StringComparison.OrdinalIgnoreCase), "Expected preview location to be the local image path.");
            }
            finally
            {
                apod_wallpaper.FileStorage.SetSessionImagesDirectory(snapshot.ImagesDirectoryPath);
                try
                {
                    if (Directory.Exists(tempDirectory))
                        Directory.Delete(tempDirectory, true);
                }
                catch
                {
                }

                RestoreSettings(snapshot);
            }
        }

        private static void ApplyLatestPublishedFallsBackAcrossVideoDays()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_smoke_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);

            var snapshot = CaptureSettings();
            try
            {
                var today = DateTime.Today;
                var yesterday = today.AddDays(-1);
                var imageDate = today.AddDays(-2);
                var localImagePath = Path.Combine(tempDirectory, imageDate.ToString("yyyy-MM-dd") + ".jpg");

                using (var bitmap = new Bitmap(12, 12))
                {
                    bitmap.Save(localImagePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                apod_wallpaper.FileStorage.SetSessionImagesDirectory(tempDirectory);

                var fakeClient = new FakeApodClient(
                    CreateVideoEntry(today),
                    new System.Collections.Generic.Dictionary<DateTime, apod_wallpaper.ApodEntry>
                    {
                        { today, CreateVideoEntry(today) },
                        { yesterday, CreateVideoEntry(yesterday) },
                        { imageDate, CreateImageEntry(imageDate) },
                    });
                var fakeCache = new InMemoryApodMetadataCache();
                var fakeWallpaperApplier = new FakeWallpaperApplier();
                var service = new apod_wallpaper.ApodWallpaperService(fakeClient, fakeCache, fakeWallpaperApplier);
                var workflow = new apod_wallpaper.ApodWorkflowService(service);

                var result = workflow.ApplyLatestPublished(apod_wallpaper.WallpaperStyle.Smart, true);

                Assert(result.Status == apod_wallpaper.ApodWorkflowStatus.Success, "Expected ApplyLatestPublished to succeed.");
                Assert(result.ResolvedDate == imageDate, "Expected ApplyLatestPublished to fall back to the nearest image date.");
                Assert(string.Equals(result.ImagePath, localImagePath, StringComparison.OrdinalIgnoreCase), "Expected fallback image path to point to the local image.");
                Assert(fakeWallpaperApplier.LastAppliedImagePath == localImagePath, "Expected wallpaper applier to receive the fallback local image.");
            }
            finally
            {
                apod_wallpaper.FileStorage.SetSessionImagesDirectory(snapshot.ImagesDirectoryPath);
                try
                {
                    if (Directory.Exists(tempDirectory))
                        Directory.Delete(tempDirectory, true);
                }
                catch
                {
                }

                RestoreSettings(snapshot);
            }
        }

        private static void SmartComposerUsesStretchForNearScreenRatio()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_smoke_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);

            try
            {
                var screenBounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
                var imagePath = Path.Combine(tempDirectory, "near-screen.jpg");

                using (var bitmap = new Bitmap(screenBounds.Width, screenBounds.Height))
                {
                    bitmap.Save(imagePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                var composition = apod_wallpaper.SmartWallpaperComposer.Prepare(imagePath);

                Assert(composition.Style == apod_wallpaper.WallpaperStyle.Stretch, "Expected near-screen-ratio image to use Stretch.");
                Assert(composition.Strategy == "stretch_near_screen_ratio", "Expected near-screen-ratio strategy.");
                Assert(string.Equals(composition.ImagePath, imagePath, StringComparison.OrdinalIgnoreCase), "Expected original image path for stretch strategy.");
            }
            finally
            {
                TryDeleteDirectory(tempDirectory);
            }
        }

        private static void SmartComposerCreatesSingleFocusForSquareImages()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_smoke_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            var snapshot = CaptureSettings();

            try
            {
                var imagePath = Path.Combine(tempDirectory, "square.jpg");

                using (var bitmap = new Bitmap(1200, 1200))
                {
                    bitmap.Save(imagePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                apod_wallpaper.FileStorage.SetSessionImagesDirectory(tempDirectory);
                var composition = apod_wallpaper.SmartWallpaperComposer.Prepare(imagePath);

                Assert(composition.Style == apod_wallpaper.WallpaperStyle.Fill, "Expected square image to use Fill after smart composition.");
                Assert(composition.Strategy == "single_focus_background", "Expected square image to use single focus strategy.");
                Assert(File.Exists(composition.ImagePath), "Expected composed smart wallpaper file to exist.");
                Assert(composition.ImagePath.IndexOf(Path.Combine("smart", string.Empty), StringComparison.OrdinalIgnoreCase) >= 0, "Expected smart image to be stored in the smart subfolder.");
            }
            finally
            {
                RestoreSettings(snapshot);
                TryDeleteDirectory(tempDirectory);
            }
        }

        private static void SchedulerUsesHourlyPollingForDemoKey()
        {
            var snapshot = CaptureSettings();
            apod_wallpaper.ApplicationController controller = null;
            try
            {
                controller = CreateController();
                var saveResult = controller.SaveSettingsAsync(new apod_wallpaper.ApplicationSettingsSnapshot
                {
                    TrayDoubleClickAction = snapshot.TrayDoubleClickAction,
                    WallpaperStyleIndex = snapshot.WallpaperStyleIndex,
                    AutoRefreshEnabled = true,
                    StartWithWindows = snapshot.StartWithWindows,
                    ImagesDirectoryPath = snapshot.ImagesDirectoryPath,
                    NasaApiKey = "DEMO_KEY",
                    NasaApiKeyValidationState = apod_wallpaper.ApiKeyValidationState.Unknown.ToString(),
                    LastAutoRefreshRunDate = snapshot.LastAutoRefreshRunDate,
                    LastAutoRefreshAppliedDate = snapshot.LastAutoRefreshAppliedDate,
                }).GetAwaiter().GetResult();
                Assert(saveResult.Succeeded, "Expected DEMO_KEY scheduler settings save to succeed.");

                Assert(controller.InitializeAsync().GetAwaiter().GetResult().Succeeded, "Expected controller initialization to succeed.");
                Assert(controller.Scheduler.PollingInterval == TimeSpan.FromHours(1), "Expected DEMO_KEY polling interval to be one hour.");
            }
            finally
            {
                if (controller != null)
                    controller.ShutdownAsync().GetAwaiter().GetResult();
                RestoreSettings(snapshot);
            }
        }

        private static void SchedulerUsesThirtyMinutePollingForPersonalKey()
        {
            var snapshot = CaptureSettings();
            apod_wallpaper.ApplicationController controller = null;
            try
            {
                controller = CreateController();
                var saveResult = controller.SaveSettingsAsync(new apod_wallpaper.ApplicationSettingsSnapshot
                {
                    TrayDoubleClickAction = snapshot.TrayDoubleClickAction,
                    WallpaperStyleIndex = snapshot.WallpaperStyleIndex,
                    AutoRefreshEnabled = true,
                    StartWithWindows = snapshot.StartWithWindows,
                    ImagesDirectoryPath = snapshot.ImagesDirectoryPath,
                    NasaApiKey = "personal-key",
                    NasaApiKeyValidationState = apod_wallpaper.ApiKeyValidationState.Valid.ToString(),
                    LastAutoRefreshRunDate = snapshot.LastAutoRefreshRunDate,
                    LastAutoRefreshAppliedDate = snapshot.LastAutoRefreshAppliedDate,
                }).GetAwaiter().GetResult();
                Assert(saveResult.Succeeded, "Expected personal-key scheduler settings save to succeed.");

                Assert(controller.InitializeAsync().GetAwaiter().GetResult().Succeeded, "Expected controller initialization to succeed.");
                Assert(controller.Scheduler.PollingInterval == TimeSpan.FromMinutes(30), "Expected personal key polling interval to be 30 minutes.");
            }
            finally
            {
                if (controller != null)
                    controller.ShutdownAsync().GetAwaiter().GetResult();
                RestoreSettings(snapshot);
            }
        }

        private static void WallpaperServiceRejectsInvalidLocalFile()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_smoke_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            var invalidImagePath = Path.Combine(tempDirectory, "broken.jpg");
            File.WriteAllText(invalidImagePath, "not-an-image");

            try
            {
                var service = new apod_wallpaper.WallpaperService();
                try
                {
                    service.ApplyPreservingHistory(invalidImagePath, apod_wallpaper.WallpaperStyle.Fill);
                    throw new InvalidOperationException("Expected wallpaper service to reject an invalid local file.");
                }
                catch (InvalidOperationException ex)
                {
                    Assert(ex.Message.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0, "Expected invalid file error message.");
                }
            }
            finally
            {
                TryDeleteDirectory(tempDirectory);
            }
        }

        private static void SchedulerDayLockSkipsRepeatedChecks()
        {
            var today = DateTime.Today;
            var shouldSkip = apod_wallpaper.ApplicationController.ShouldSkipSchedulerForToday(today, today.AddDays(-1), today);
            Assert(shouldSkip, "Expected scheduler day lock to skip repeated checks when today's run already completed.");
        }

        private static void SchedulerDayLockRequiresAppliedDate()
        {
            var today = DateTime.Today;
            var shouldSkip = apod_wallpaper.ApplicationController.ShouldSkipSchedulerForToday(today, null, today);
            Assert(!shouldSkip, "Expected scheduler day lock to require a successfully applied date.");
        }

        private static void ApiKeyIsStoredOutsidePlaintextSettings()
        {
            var snapshot = CaptureSettings();
            try
            {
                ResetSecretStore();
                var controller = CreateController();
                var saveResult = controller.SaveSettingsAsync(new apod_wallpaper.ApplicationSettingsSnapshot
                {
                    TrayDoubleClickAction = snapshot.TrayDoubleClickAction,
                    WallpaperStyleIndex = snapshot.WallpaperStyleIndex,
                    AutoRefreshEnabled = snapshot.AutoRefreshEnabled,
                    StartWithWindows = snapshot.StartWithWindows,
                    ImagesDirectoryPath = snapshot.ImagesDirectoryPath,
                    NasaApiKey = "protected-test-key",
                    NasaApiKeyValidationState = apod_wallpaper.ApiKeyValidationState.Valid.ToString(),
                    LastAutoRefreshRunDate = snapshot.LastAutoRefreshRunDate,
                    LastAutoRefreshAppliedDate = snapshot.LastAutoRefreshAppliedDate,
                }).GetAwaiter().GetResult();

                Assert(saveResult.Succeeded, "Expected protected API key save to succeed.");
                Assert(apod_wallpaper.UserSecretStore.GetNasaApiKey() == "protected-test-key", "Expected API key to be stored in protected storage.");
                Assert(string.IsNullOrWhiteSpace(apod_wallpaper.Properties.Settings.Default.NasaApiKey), "Expected plaintext settings slot to stay empty.");
                Assert(GetValueOrThrow(controller.GetSettingsAsync().GetAwaiter().GetResult(), "Unable to read settings after protected save.").NasaApiKey == "protected-test-key", "Expected facade settings to surface the protected API key.");
            }
            finally
            {
                RestoreSettings(snapshot);
                ResetSecretStore();
            }
        }

        private static void LegacyApiKeyMigratesToProtectedStorage()
        {
            var snapshot = CaptureSettings();
            try
            {
                ResetSecretStore();
                apod_wallpaper.Properties.Settings.Default.NasaApiKey = "legacy-plaintext-key";
                apod_wallpaper.Properties.Settings.Default.Save();

                var controller = CreateController();
                var initialization = controller.InitializeAsync().GetAwaiter().GetResult();
                Assert(initialization.Succeeded, "Expected controller initialization to succeed during legacy API key migration.");
                Assert(apod_wallpaper.UserSecretStore.GetNasaApiKey() == "legacy-plaintext-key", "Expected legacy API key to migrate into protected storage.");
                Assert(string.IsNullOrWhiteSpace(apod_wallpaper.Properties.Settings.Default.NasaApiKey), "Expected legacy plaintext setting to be cleared after migration.");
            }
            finally
            {
                RestoreSettings(snapshot);
                ResetSecretStore();
            }
        }

        private static void InitialStateSnapshotReturnsStartupData()
        {
            var snapshot = CaptureSettings();
            var customImagesDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_initial_state_images_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(customImagesDirectory);

            try
            {
                var lastAppliedDate = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");
                var controller = CreateController();
                var saveResult = controller.SaveSettingsAsync(new apod_wallpaper.ApplicationSettingsSnapshot
                {
                    TrayDoubleClickAction = true,
                    WallpaperStyleIndex = (int)apod_wallpaper.WallpaperStyle.Fill,
                    AutoRefreshEnabled = snapshot.AutoRefreshEnabled,
                    StartWithWindows = snapshot.StartWithWindows,
                    ImagesDirectoryPath = customImagesDirectory,
                    NasaApiKey = "DEMO_KEY",
                    NasaApiKeyValidationState = apod_wallpaper.ApiKeyValidationState.Unknown.ToString(),
                    LastAutoRefreshRunDate = snapshot.LastAutoRefreshRunDate,
                    LastAutoRefreshAppliedDate = lastAppliedDate,
                }).GetAwaiter().GetResult();
                Assert(saveResult.Succeeded, "Expected initial state settings save to succeed.");

                var initialStateResult = controller.GetInitialStateAsync().GetAwaiter().GetResult();
                Assert(initialStateResult.Succeeded, "Expected initial state snapshot to succeed.");

                var initialState = initialStateResult.Value;
                Assert(initialState != null, "Expected initial state payload.");
                Assert(initialState.Settings != null, "Expected settings inside initial state.");
                Assert(initialState.StoragePaths != null, "Expected storage paths inside initial state.");
                Assert(initialState.ApiKeyValidationState == apod_wallpaper.ApiKeyValidationState.Unknown, "Expected validation state from initial state.");
                Assert(initialState.PreferredDisplayDate == DateTime.Today.AddDays(-1), "Expected preferred display date from initial state.");
                Assert(initialState.SelectedWallpaperStyle == apod_wallpaper.WallpaperStyle.Fill, "Expected selected wallpaper style from initial state.");
                Assert(initialState.LocalImageIndexReady, "Expected initial state to confirm local index readiness.");
                Assert(string.Equals(initialState.StoragePaths.ImagesDirectory, customImagesDirectory, StringComparison.OrdinalIgnoreCase), "Expected initial state to surface effective images directory.");
                Assert(string.Equals(initialState.Settings.ImagesDirectoryPath, customImagesDirectory, StringComparison.OrdinalIgnoreCase), "Expected initial state settings to keep configured images directory.");
            }
            finally
            {
                RestoreSettings(snapshot);
                TryDeleteDirectory(customImagesDirectory);
            }
        }

        private static void JsonSettingsStoreWritesSettingsFile()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_settings_store_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            var settingsPath = Path.Combine(tempDirectory, "settings.json");

            try
            {
                var store = new apod_wallpaper.JsonSettingsStore(settingsPath, new apod_wallpaper.LegacyPropertiesSettingsBridge());
                var snapshot = new apod_wallpaper.ApplicationSettingsSnapshot
                {
                    TrayDoubleClickAction = true,
                    WallpaperStyleIndex = (int)apod_wallpaper.WallpaperStyle.Fill,
                    AutoRefreshEnabled = true,
                    StartWithWindows = false,
                    NasaApiKeyValidationState = apod_wallpaper.ApiKeyValidationState.Valid.ToString(),
                    ImagesDirectoryPath = @"C:\temp\images",
                    LastAutoRefreshRunDate = "2026-04-29",
                    LastAutoRefreshAppliedDate = "2026-04-28",
                };

                store.Save(snapshot);

                Assert(File.Exists(settingsPath), "Expected settings.json to be created.");
                var json = File.ReadAllText(settingsPath);
                Assert(json.IndexOf("TrayDoubleClickAction", StringComparison.OrdinalIgnoreCase) >= 0, "Expected non-secret settings inside settings.json.");
                Assert(json.IndexOf("ImagesDirectoryPath", StringComparison.OrdinalIgnoreCase) >= 0, "Expected images directory inside settings.json.");
                Assert(json.IndexOf("protected-test-key", StringComparison.OrdinalIgnoreCase) < 0, "Expected API key secret value not to be written into settings.json.");

                var loaded = store.Load();
                Assert(loaded.TrayDoubleClickAction, "Expected tray action to round-trip through settings.json.");
                Assert(loaded.WallpaperStyleIndex == (int)apod_wallpaper.WallpaperStyle.Fill, "Expected wallpaper style to round-trip through settings.json.");
                Assert(loaded.AutoRefreshEnabled, "Expected auto-refresh flag to round-trip through settings.json.");
                Assert(!loaded.StartWithWindows, "Expected start-with-Windows flag to round-trip through settings.json.");
                Assert(loaded.ImagesDirectoryPath == @"C:\temp\images", "Expected images directory to round-trip through settings.json.");
            }
            finally
            {
                TryDeleteDirectory(tempDirectory);
            }
        }

        private static void LegacySettingsMigrateToJsonStore()
        {
            var snapshot = CaptureSettings();
            var tempDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_settings_migration_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            var settingsPath = Path.Combine(tempDirectory, "settings.json");

            try
            {
                apod_wallpaper.Properties.Settings.Default.TrayDoubleClickAction = true;
                apod_wallpaper.Properties.Settings.Default.StyleComboBox = (int)apod_wallpaper.WallpaperStyle.Tile;
                apod_wallpaper.Properties.Settings.Default.AutoRefreshEnabled = true;
                apod_wallpaper.Properties.Settings.Default.StartWithWindows = false;
                apod_wallpaper.Properties.Settings.Default.NasaApiKeyValidationState = apod_wallpaper.ApiKeyValidationState.Invalid.ToString();
                apod_wallpaper.Properties.Settings.Default.ImagesDirectoryPath = @"C:\legacy\images";
                apod_wallpaper.Properties.Settings.Default.LastAutoRefreshRunDate = "2026-04-20";
                apod_wallpaper.Properties.Settings.Default.LastAutoRefreshAppliedDate = "2026-04-19";
                apod_wallpaper.Properties.Settings.Default.NasaApiKey = "legacy-secret-key";
                apod_wallpaper.Properties.Settings.Default.Save();

                var store = new apod_wallpaper.JsonSettingsStore(settingsPath, new apod_wallpaper.LegacyPropertiesSettingsBridge());
                store.MigrateLegacySettingsIfNeeded();

                Assert(File.Exists(settingsPath), "Expected migration to create settings.json.");
                var migrated = store.Load();
                Assert(migrated.TrayDoubleClickAction, "Expected tray action to migrate into json store.");
                Assert(migrated.WallpaperStyleIndex == (int)apod_wallpaper.WallpaperStyle.Tile, "Expected wallpaper style to migrate into json store.");
                Assert(migrated.AutoRefreshEnabled, "Expected auto-refresh to migrate into json store.");
                Assert(!migrated.StartWithWindows, "Expected start-with-Windows to migrate into json store.");
                Assert(migrated.NasaApiKeyValidationState == apod_wallpaper.ApiKeyValidationState.Invalid.ToString(), "Expected validation state to migrate into json store.");
                Assert(migrated.ImagesDirectoryPath == @"C:\legacy\images", "Expected images directory to migrate into json store.");
                Assert(migrated.LastAutoRefreshRunDate == "2026-04-20", "Expected last run date to migrate into json store.");
                Assert(migrated.LastAutoRefreshAppliedDate == "2026-04-19", "Expected last applied date to migrate into json store.");
                Assert(string.IsNullOrWhiteSpace(apod_wallpaper.Properties.Settings.Default.ImagesDirectoryPath), "Expected legacy non-secret settings to be cleared after migration.");
                Assert(apod_wallpaper.Properties.Settings.Default.NasaApiKey == "legacy-secret-key", "Expected legacy secret key to stay until DPAPI migration handles it.");
            }
            finally
            {
                RestoreSettings(snapshot);
                TryDeleteDirectory(tempDirectory);
            }
        }

        private static void StorageLayoutResolvesAllPathsCentrally()
        {
            var snapshot = CaptureSettings();
            var customImagesDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_storage_images_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(customImagesDirectory);

            try
            {
                apod_wallpaper.FileStorage.SetStorageModeOverride(apod_wallpaper.ApplicationStorageMode.LocalApplicationData);

                var controller = CreateController();
                var saveResult = controller.SaveSettingsAsync(new apod_wallpaper.ApplicationSettingsSnapshot
                {
                    TrayDoubleClickAction = snapshot.TrayDoubleClickAction,
                    WallpaperStyleIndex = snapshot.WallpaperStyleIndex,
                    AutoRefreshEnabled = snapshot.AutoRefreshEnabled,
                    StartWithWindows = snapshot.StartWithWindows,
                    ImagesDirectoryPath = customImagesDirectory,
                    NasaApiKey = snapshot.NasaApiKey,
                    NasaApiKeyValidationState = snapshot.NasaApiKeyValidationState,
                    LastAutoRefreshRunDate = snapshot.LastAutoRefreshRunDate,
                    LastAutoRefreshAppliedDate = snapshot.LastAutoRefreshAppliedDate,
                }).GetAwaiter().GetResult();
                Assert(saveResult.Succeeded, "Expected storage settings save to succeed.");

                var layout = GetValueOrThrow(controller.GetStoragePathsAsync().GetAwaiter().GetResult(), "Unable to read storage layout.");
                Assert(string.Equals(layout.ImagesDirectory, customImagesDirectory, StringComparison.OrdinalIgnoreCase), "Expected custom images directory in storage layout.");
                Assert(string.Equals(layout.SmartImagesDirectory, Path.Combine(customImagesDirectory, "smart"), StringComparison.OrdinalIgnoreCase), "Expected smart directory under images directory.");
                Assert(layout.CacheDirectory.IndexOf("apod_wallpaper", StringComparison.OrdinalIgnoreCase) >= 0, "Expected cache directory to be backend-defined.");
                Assert(layout.LogsDirectory.IndexOf("apod_wallpaper", StringComparison.OrdinalIgnoreCase) >= 0, "Expected logs directory to be backend-defined.");
                Assert(layout.SecretsDirectory.IndexOf("secrets", StringComparison.OrdinalIgnoreCase) >= 0, "Expected secrets directory to be backend-defined.");
                Assert(layout.SettingsFilePath.IndexOf("settings.json", StringComparison.OrdinalIgnoreCase) >= 0, "Expected settings.json path to be backend-defined.");
                Assert(layout.Mode == apod_wallpaper.ApplicationStorageMode.LocalApplicationData, "Expected local application data mode.");
                Assert(layout.UsesCustomImagesDirectory, "Expected storage layout to report custom images directory usage.");
            }
            finally
            {
                apod_wallpaper.FileStorage.SetStorageModeOverride(null);
                RestoreSettings(snapshot);
                TryDeleteDirectory(customImagesDirectory);
            }
        }

        private static void PortableStorageModeUsesPortableLayout()
        {
            apod_wallpaper.FileStorage.SetStorageModeOverride(apod_wallpaper.ApplicationStorageMode.Portable);
            try
            {
                apod_wallpaper.FileStorage.SetSessionImagesDirectory(null);
                apod_wallpaper.AppRuntimeSettings.Configure(null, null, apod_wallpaper.ApiKeyValidationState.Unknown);
                var controller = CreateController();
                var layout = GetValueOrThrow(controller.GetStoragePathsAsync().GetAwaiter().GetResult(), "Unable to read portable storage layout.");
                var expectedImagesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images");
                var expectedApplicationDataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");

                Assert(layout.Mode == apod_wallpaper.ApplicationStorageMode.Portable, "Expected portable storage mode.");
                Assert(string.Equals(layout.ImagesDirectory, expectedImagesDirectory, StringComparison.OrdinalIgnoreCase), "Expected portable images directory next to executable.");
                Assert(string.Equals(layout.ApplicationDataDirectory, expectedApplicationDataDirectory, StringComparison.OrdinalIgnoreCase), "Expected portable application data directory next to executable.");
                Assert(string.Equals(layout.CacheDirectory, Path.Combine(expectedApplicationDataDirectory, "cache"), StringComparison.OrdinalIgnoreCase), "Expected portable cache directory.");
                Assert(string.Equals(layout.LogsDirectory, Path.Combine(expectedApplicationDataDirectory, "logs"), StringComparison.OrdinalIgnoreCase), "Expected portable logs directory.");
                Assert(string.Equals(layout.SecretsDirectory, Path.Combine(expectedApplicationDataDirectory, "secrets"), StringComparison.OrdinalIgnoreCase), "Expected portable secrets directory.");
                Assert(string.Equals(layout.SettingsFilePath, Path.Combine(expectedApplicationDataDirectory, "settings.json"), StringComparison.OrdinalIgnoreCase), "Expected portable settings.json path.");
            }
            finally
            {
                apod_wallpaper.FileStorage.SetStorageModeOverride(null);
            }
        }

        private static void PublicFacadeMethodsUseOperationResults()
        {
            var facadeType = typeof(apod_wallpaper.IApplicationBackendFacade);
            var invalidMethods = facadeType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => !UsesOperationResultContract(method.ReturnType))
                .Select(method => method.Name + ": " + method.ReturnType.FullName)
                .ToArray();

            Assert(invalidMethods.Length == 0,
                "Expected all public facade methods to use OperationResult contracts. Invalid methods: " + string.Join(", ", invalidMethods));
        }

        private static void FailedWorkflowStatusMapsToOperationError()
        {
            var failedResult = new apod_wallpaper.ApodWorkflowResult
            {
                Status = apod_wallpaper.ApodWorkflowStatus.Failed,
                Message = "Workflow-level failure should surface as an operation error.",
            };

            try
            {
                apod_wallpaper.ApplicationController.EnsureWorkflowResultSucceeded(
                    failedResult,
                    "Fallback workflow failure message.");
                throw new InvalidOperationException("Expected failed workflow status to be rejected before reaching the public facade payload.");
            }
            catch (InvalidOperationException ex)
            {
                Assert(ex.Message == failedResult.Message, "Expected workflow failure message to become the public operation error message.");
            }

            var unavailableResult = new apod_wallpaper.ApodWorkflowResult
            {
                Status = apod_wallpaper.ApodWorkflowStatus.Unavailable,
                Message = "Unavailable is a valid domain outcome.",
            };

            var mappedUnavailable = apod_wallpaper.ApplicationController.EnsureWorkflowResultSucceeded(
                unavailableResult,
                "Fallback workflow failure message.");
            Assert(object.ReferenceEquals(mappedUnavailable, unavailableResult), "Expected unavailable workflow result to remain a successful payload.");
        }

        private static void BackendFacadeDoesNotExposeDiagnosticsContract()
        {
            Assert(!typeof(apod_wallpaper.IApplicationDiagnosticsFacade).IsAssignableFrom(typeof(apod_wallpaper.IApplicationBackendFacade)),
                "Expected backend facade to stop exposing diagnostics methods directly to frontend callers.");
        }

        private static bool UsesOperationResultContract(Type returnType)
        {
            if (returnType.IsGenericType &&
                returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var taskInnerType = returnType.GetGenericArguments()[0];
                if (taskInnerType == typeof(apod_wallpaper.OperationResult))
                    return true;

                if (taskInnerType.IsGenericType &&
                    taskInnerType.GetGenericTypeDefinition() == typeof(apod_wallpaper.OperationResult<>))
                {
                    return true;
                }
            }

            return false;
        }

        private static void WallpaperAppliedSubscriptionDisposesCleanly()
        {
            var controller = CreateController();
            EventHandler<apod_wallpaper.WallpaperAppliedEventArgs> handler = delegate { };

            var subscribeResult = controller.SubscribeWallpaperAppliedAsync(handler).GetAwaiter().GetResult();
            Assert(subscribeResult.Succeeded, "Expected wallpaper subscription to succeed.");
            Assert(subscribeResult.Value != null, "Expected wallpaper subscription token.");

            var eventField = typeof(apod_wallpaper.ApplicationController).GetField(
                "WallpaperApplied",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(eventField != null, "Expected to inspect WallpaperApplied backing field.");

            var afterSubscribe = eventField.GetValue(controller) as Delegate;
            Assert(afterSubscribe != null && afterSubscribe.GetInvocationList().Length == 1,
                "Expected one wallpaper handler after subscription.");

            subscribeResult.Value.Dispose();

            var afterDispose = eventField.GetValue(controller) as Delegate;
            Assert(afterDispose == null || afterDispose.GetInvocationList().Length == 0,
                "Expected wallpaper handler to be removed after disposing subscription.");
        }

        private static apod_wallpaper.ApplicationSettingsSnapshot CaptureSettings()
        {
            return GetValueOrThrow(CreateController().GetSettingsAsync().GetAwaiter().GetResult(), "Unable to capture current application settings.");
        }

        private static void RestoreSettings(apod_wallpaper.ApplicationSettingsSnapshot snapshot)
        {
            var restoreResult = CreateController().SaveSettingsAsync(snapshot).GetAwaiter().GetResult();
            Assert(restoreResult.Succeeded, "Expected settings restore to succeed.");
        }

        private static apod_wallpaper.ApplicationController CreateController()
        {
            return new apod_wallpaper.ApplicationController(
                new apod_wallpaper.JsonSettingsStore(),
                new apod_wallpaper.StartupService());
        }

        private static T GetValueOrThrow<T>(apod_wallpaper.OperationResult<T> result, string fallbackMessage)
        {
            if (result == null)
                throw new InvalidOperationException(fallbackMessage);

            if (!result.Succeeded)
                throw new InvalidOperationException(result.Error != null ? result.Error.Message : fallbackMessage);

            return result.Value;
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

        private static void ResetSecretStore()
        {
            TryDeleteDirectory(_secretStoreDirectory);
            Directory.CreateDirectory(_secretStoreDirectory);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static apod_wallpaper.ApodEntry CreateVideoEntry(DateTime date)
        {
            return new apod_wallpaper.ApodEntry
            {
                Date = date.ToString("yyyy-MM-dd"),
                MediaType = "video",
                Url = "https://example.test/video.mp4",
                HdUrl = null,
                ResolvedFromSource = "api",
            };
        }

        private static apod_wallpaper.ApodEntry CreateImageEntry(DateTime date)
        {
            return new apod_wallpaper.ApodEntry
            {
                Date = date.ToString("yyyy-MM-dd"),
                MediaType = "image",
                Url = "https://example.test/" + date.ToString("yyyy-MM-dd") + "_preview.jpg",
                HdUrl = "https://example.test/" + date.ToString("yyyy-MM-dd") + "_full.jpg",
                ResolvedFromSource = "api",
            };
        }

        private sealed class FakeApodClient : apod_wallpaper.IApodClient
        {
            private readonly apod_wallpaper.ApodEntry _latestEntry;
            private readonly System.Collections.Generic.Dictionary<DateTime, apod_wallpaper.ApodEntry> _entries;

            public FakeApodClient(apod_wallpaper.ApodEntry latestEntry, System.Collections.Generic.Dictionary<DateTime, apod_wallpaper.ApodEntry> entries)
            {
                _latestEntry = latestEntry;
                _entries = entries;
            }

            public apod_wallpaper.ApodEntry GetEntry(DateTime date) => _entries[date.Date];
            public Task<apod_wallpaper.ApodEntry> GetEntryAsync(DateTime date) => Task.FromResult(GetEntry(date));
            public apod_wallpaper.ApodEntry GetLatestEntry() => _latestEntry;
            public Task<apod_wallpaper.ApodEntry> GetLatestEntryAsync() => Task.FromResult(_latestEntry);
            public System.Collections.Generic.IReadOnlyList<apod_wallpaper.ApodEntry> GetEntries(DateTime startDate, DateTime endDate) => new[] { _latestEntry };
            public Task<System.Collections.Generic.IReadOnlyList<apod_wallpaper.ApodEntry>> GetEntriesAsync(DateTime startDate, DateTime endDate) => Task.FromResult(GetEntries(startDate, endDate));
            public Task<apod_wallpaper.ApiKeyValidationState> ValidateApiKeyAsync(string apiKey) => Task.FromResult(apod_wallpaper.ApiKeyValidationState.Valid);
        }

        private sealed class InMemoryApodMetadataCache : apod_wallpaper.IApodMetadataCache
        {
            private readonly System.Collections.Generic.Dictionary<DateTime, apod_wallpaper.ApodCachedEntry> _entries = new System.Collections.Generic.Dictionary<DateTime, apod_wallpaper.ApodCachedEntry>();

            public apod_wallpaper.ApodCachedEntry Get(DateTime date)
            {
                apod_wallpaper.ApodCachedEntry entry;
                _entries.TryGetValue(date.Date, out entry);
                return entry;
            }

            public System.Collections.Generic.IReadOnlyList<apod_wallpaper.ApodCachedEntry> GetRange(DateTime startDate, DateTime endDate)
            {
                return _entries.Values.ToArray();
            }

            public void Upsert(apod_wallpaper.ApodEntry entry)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Date))
                    return;

                _entries[DateTime.Parse(entry.Date).Date] = apod_wallpaper.ApodCachedEntry.FromEntry(entry);
            }

            public void UpsertRange(System.Collections.Generic.IEnumerable<apod_wallpaper.ApodEntry> entries)
            {
                foreach (var entry in entries)
                    Upsert(entry);
            }

            public void SaveLocalImagePath(DateTime date, string localImagePath)
            {
                apod_wallpaper.ApodCachedEntry entry;
                if (_entries.TryGetValue(date.Date, out entry))
                    entry.LocalImagePath = localImagePath;
            }

            public void SyncLocalImagePaths()
            {
            }
        }

        private sealed class FakeWallpaperApplier : apod_wallpaper.IWallpaperApplier
        {
            public string LastAppliedImagePath { get; private set; }
            public apod_wallpaper.WallpaperStyle LastAppliedStyle { get; private set; }

            public void ApplyPreservingHistory(string imagePath, apod_wallpaper.WallpaperStyle style)
            {
                LastAppliedImagePath = imagePath;
                LastAppliedStyle = style;
            }
        }
    }
}
