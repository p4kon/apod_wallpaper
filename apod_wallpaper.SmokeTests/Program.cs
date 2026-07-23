using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace apod_wallpaper.SmokeTests
{
    internal static class Program
    {
        private static int _failures;
        private static string _secretStoreDirectory;
        private static string _logDirectory;
        private static InMemorySettingsStore _settingsStore;
        private static apod_wallpaper.DpapiUserSecretStore _secretStore;

        [STAThread]
        private static int Main()
        {
            _secretStoreDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_smoke_secrets_" + Guid.NewGuid().ToString("N"));
            _logDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_smoke_logs_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_secretStoreDirectory);
            Directory.CreateDirectory(_logDirectory);
            apod_wallpaper.AppLogger.SetLogDirectoryOverride(_logDirectory);
            _settingsStore = new InMemorySettingsStore(CreateDefaultSettingsSnapshot());
            _secretStore = new apod_wallpaper.DpapiUserSecretStore(_secretStoreDirectory);

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
                Run("HTML extractor resolves title and explanation text", HtmlExtractorResolvesTextMetadata);
                Run("Runtime settings fall back to DEMO_KEY for invalid key", InvalidApiKeyFallsBackToDemoKey);
                Run("Local image is preferred for preview", LocalImageIsPreferredForPreview);
                Run("ApplyLatestPublished walks back through video days", ApplyLatestPublishedFallsBackAcrossVideoDays);
                Run("Smart composer stretches near screen ratio images", SmartComposerUsesStretchForNearScreenRatio);
                Run("Smart composer creates single focus image for square content", SmartComposerCreatesSingleFocusForSquareImages);
                Run("Smart composer preserves ultrawide images without Fill cropping", SmartComposerPreservesUltraWideImages);
                Run("Scheduler uses hourly polling for DEMO_KEY", SchedulerUsesHourlyPollingForDemoKey);
                Run("Scheduler uses 30 minute polling for personal key", SchedulerUsesThirtyMinutePollingForPersonalKey);
                Run("Wallpaper service rejects invalid local file", WallpaperServiceRejectsInvalidLocalFile);
                Run("Scheduler day lock skips repeated checks after today's image", SchedulerDayLockSkipsAfterTodaysImage);
                Run("Scheduler day lock keeps checking after yesterday fallback", SchedulerDayLockKeepsCheckingAfterYesterdayFallback);
                Run("Scheduler day lock does not skip when no applied date", SchedulerDayLockRequiresAppliedDate);
                Run("API key is stored outside plaintext settings", ApiKeyIsStoredOutsidePlaintextSettings);
                Run("Initial state snapshot returns startup data in one call", InitialStateSnapshotReturnsStartupData);
                Run("Json settings store writes non-secret settings to settings.json", JsonSettingsStoreWritesSettingsFile);
                Run("Storage layout resolves all backend paths centrally", StorageLayoutResolvesAllPathsCentrally);
                Run("Storage summary counts local library without cleanup", StorageSummaryCountsLocalLibraryWithoutCleanup);
                Run("Portable storage mode keeps app data near executable", PortableStorageModeUsesPortableLayout);
                Run("Store storage mode keeps app data inside sandbox path", StoreStorageModeUsesSandboxLayout);
                Run("Public facade methods use operation results", PublicFacadeMethodsUseOperationResults);
                Run("Public workflow payload never exposes failed status", FailedWorkflowStatusMapsToOperationError);
                Run("Backend facade does not expose diagnostics contract", BackendFacadeDoesNotExposeDiagnosticsContract);
                Run("WallpaperApplied subscription disposes cleanly", WallpaperAppliedSubscriptionDisposesCleanly);
                Run("WinUI localization literals are covered", WinUiLocalizationLiteralsAreCovered);
                Run("Translation target language normalizes values", TranslationTargetLanguageNormalizesValues);
                Run("Google Translate URL builder encodes explanation", GoogleTranslateUrlBuilderEncodesExplanation);
                Run("APOD page URL builder is deterministic", ApodPageUrlBuilderIsDeterministic);
                Run("APOD availability probe evaluates redirects", ApodAvailabilityProbeEvaluatesRedirects);
                Run("APOD availability probe retries forbidden HEAD with GET", ApodAvailabilityProbeRetriesForbiddenHeadWithGet);
                Run("Calendar availability transient override unlocks today only", CalendarAvailabilityTransientOverrideUnlocksTodayOnly);
                Run("Calendar availability throttle resets when today changes", CalendarAvailabilityThrottleResetsWhenTodayChanges);
                Run("APOD availability probe source avoids workflow side effects", ApodAvailabilityProbeSourceAvoidsWorkflowSideEffects);
                Run("Favorite APOD store persists normalized dates", FavoriteApodStorePersistsNormalizedDates);
                Run("Update check compares release versions", UpdateCheckComparesReleaseVersions);
                Run("Update check defaults to automatic checks enabled", UpdateCheckDefaultsToAutomaticChecksEnabled);
                Run("Random APOD settings and sources normalize", RandomApodSettingsAndSourcesNormalize);
                Run("Downloaded APOD date scan ignores smart artifacts", DownloadedApodDateScanIgnoresSmartArtifacts);

                Console.WriteLine(_failures == 0
                    ? "Smoke tests passed."
                    : "Smoke tests failed: " + _failures);

                return _failures == 0 ? 0 : 1;
            }
            finally
            {
                apod_wallpaper.AppLogger.ClearLogDirectoryOverride();
                TryDeleteDirectory(_secretStoreDirectory);
                TryDeleteDirectory(_logDirectory);
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

        private static void FavoriteApodStorePersistsNormalizedDates()
        {
            var directory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_favorites_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var path = Path.Combine(directory, "favorites.json");
                var store = new apod_wallpaper.FavoriteApodStore(path);
                var date = new DateTime(2026, 7, 14);

                Assert(store.SetFavorite(date, true), "Initial favorite add should change state.");
                Assert(!store.SetFavorite(date, true), "Duplicate favorite add should not change state.");
                Assert(store.IsFavorite(date), "Favorite date should be present.");

                var reloaded = new apod_wallpaper.FavoriteApodStore(path);
                var dates = reloaded.GetDates();
                Assert(dates.Count == 1 && dates[0] == date.Date, "Favorite date should survive reload once.");

                Assert(reloaded.SetFavorite(date, false), "Favorite remove should change state.");
                Assert(!reloaded.IsFavorite(date), "Favorite date should be removed.");
            }
            finally
            {
                TryDeleteDirectory(directory);
            }
        }

        private static void UpdateCheckComparesReleaseVersions()
        {
            Assert(apod_wallpaper.UpdateCheckService.NormalizeVersionText("v1.2.1") == "1.2.1", "Expected v-prefix to be ignored.");
            Assert(apod_wallpaper.UpdateCheckService.CompareReleaseVersions("v1.2.2", "1.2.1") > 0, "Expected newer release to compare higher.");
            Assert(apod_wallpaper.UpdateCheckService.CompareReleaseVersions("1.2.1", "v1.2.1") == 0, "Expected equal versions to compare equal.");
            Assert(apod_wallpaper.UpdateCheckService.CompareReleaseVersions("1.2.0", "1.2.1") < 0, "Expected older release to compare lower.");
        }

        private static void UpdateCheckDefaultsToAutomaticChecksEnabled()
        {
            var directory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_update_settings_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var store = new apod_wallpaper.JsonSettingsStore(Path.Combine(directory, "settings.json"));
                var defaults = store.Load();
                Assert(defaults.AutoCheckUpdatesEnabled, "Fresh settings should enable update auto-checks.");
                Assert(!defaults.SuppressAutomaticUpdateReminder, "Fresh settings should not suppress update reminders.");

                defaults.AutoCheckUpdatesEnabled = false;
                defaults.SuppressAutomaticUpdateReminder = true;
                defaults.LastUpdateCheckUtc = new DateTime(2026, 7, 23, 0, 0, 0, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);
                store.Save(defaults);

                var reloaded = store.Load();
                Assert(!reloaded.AutoCheckUpdatesEnabled, "Saved update auto-check setting should round-trip.");
                Assert(reloaded.SuppressAutomaticUpdateReminder, "Saved reminder suppression should round-trip.");
                Assert(!string.IsNullOrWhiteSpace(reloaded.LastUpdateCheckUtc), "Last update check timestamp should round-trip.");
            }
            finally
            {
                TryDeleteDirectory(directory);
            }
        }

        private static void RandomApodSettingsAndSourcesNormalize()
        {
            Assert(apod_wallpaper.RandomApodSource.Normalize(null) == apod_wallpaper.RandomApodSource.Global, "Expected null random source to normalize to global.");
            Assert(apod_wallpaper.RandomApodSource.Normalize(string.Empty) == apod_wallpaper.RandomApodSource.Global, "Expected empty random source to normalize to global.");
            Assert(apod_wallpaper.RandomApodSource.Normalize("local") == apod_wallpaper.RandomApodSource.Downloaded, "Expected local alias to normalize to downloaded.");
            Assert(apod_wallpaper.RandomApodSource.Normalize("favorite") == apod_wallpaper.RandomApodSource.Favorites, "Expected favorite alias to normalize to favorites.");
            Assert(apod_wallpaper.RandomApodService.ResolveGlobalStartDate(false) == new DateTime(2015, 1, 1), "Expected default global random range to start in 2015.");
            Assert(apod_wallpaper.RandomApodService.ResolveGlobalStartDate(true) == new DateTime(1995, 6, 16), "Expected deep archive random range to start at first APOD page.");

            var defaults = apod_wallpaper.JsonSettingsStore.CreateDefaultSnapshot();
            Assert(defaults.RandomApodSource == apod_wallpaper.RandomApodSource.Global, "Expected default random source to be global.");
            Assert(!defaults.RandomApodIncludeDeepArchive, "Expected deep archive random mode to be off by default.");
        }

        private static void DownloadedApodDateScanIgnoresSmartArtifacts()
        {
            var directory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_downloaded_dates_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            Directory.CreateDirectory(Path.Combine(directory, "smart"));
            var snapshot = CaptureSettings();
            try
            {
                var localDate = new DateTime(2026, 7, 14);
                using (var bitmap = new Bitmap(4, 4))
                {
                    bitmap.Save(Path.Combine(directory, "2026-07-14.jpg"), System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                File.WriteAllText(Path.Combine(directory, "notes.txt"), "ignore");
                File.WriteAllText(Path.Combine(directory, "smart", "2026-07-15.jpg"), "ignore");

                apod_wallpaper.FileStorage.SetSessionImagesDirectory(directory);
                var dates = apod_wallpaper.FileStorage.GetDownloadedImageDates();
                Assert(dates.Count == 1 && dates[0] == localDate, "Expected only top-level APOD image filename dates.");
            }
            finally
            {
                apod_wallpaper.FileStorage.SetSessionImagesDirectory(snapshot.ImagesDirectoryPath);
                TryDeleteDirectory(directory);
            }
        }

        private static void ApodPageUrlBuilderIsDeterministic()
        {
            var url = apod_wallpaper.ApodPageUrl.BuildUrl(new DateTime(2026, 7, 14));
            Assert(url == "https://apod.nasa.gov/apod/ap260714.html", "Expected APOD HTML URL to use apYYMMDD.html.");

            var firstApodUrl = apod_wallpaper.ApodPageUrl.BuildUrl(new DateTime(1995, 6, 16));
            Assert(firstApodUrl == "https://apod.nasa.gov/apod/ap950616.html", "Expected APOD HTML URL to preserve two-digit year formatting.");
        }

        private static void ApodAvailabilityProbeEvaluatesRedirects()
        {
            var requestedDate = new DateTime(2026, 7, 15);
            var expectedUrl = apod_wallpaper.ApodPageUrl.BuildUrl(requestedDate);

            var available = apod_wallpaper.ApodPageAvailabilityProbe.EvaluateResponse(
                requestedDate,
                expectedUrl,
                System.Net.HttpStatusCode.OK,
                "HEAD",
                null,
                new Uri(expectedUrl));
            Assert(available.IsAvailable, "Expected exact 200 response to be available.");

            var redirected = apod_wallpaper.ApodPageAvailabilityProbe.EvaluateResponse(
                requestedDate,
                expectedUrl,
                System.Net.HttpStatusCode.Redirect,
                "HEAD",
                new Uri("/apod/ap260714.html", UriKind.Relative),
                new Uri(expectedUrl));
            Assert(redirected.IsUnavailable, "Expected redirect to another APOD date to be unavailable.");
            Assert(!redirected.IsAvailable, "Redirect to another date must not unlock today.");
            Assert(!apod_wallpaper.ApodPageAvailabilityProbe.ShouldRetryWithGet(redirected), "Redirect responses must not retry as GET.");
        }

        private static void ApodAvailabilityProbeRetriesForbiddenHeadWithGet()
        {
            var requestedDate = new DateTime(2026, 7, 15);
            var expectedUrl = apod_wallpaper.ApodPageUrl.BuildUrl(requestedDate);

            var forbiddenHead = apod_wallpaper.ApodPageAvailabilityProbe.EvaluateResponse(
                requestedDate,
                expectedUrl,
                System.Net.HttpStatusCode.Forbidden,
                "HEAD",
                null,
                new Uri(expectedUrl));
            Assert(apod_wallpaper.ApodPageAvailabilityProbe.ShouldRetryWithGet(forbiddenHead), "Expected HEAD 403 to retry with GET.");

            var notFoundHead = apod_wallpaper.ApodPageAvailabilityProbe.EvaluateResponse(
                requestedDate,
                expectedUrl,
                System.Net.HttpStatusCode.NotFound,
                "HEAD",
                null,
                new Uri(expectedUrl));
            Assert(!apod_wallpaper.ApodPageAvailabilityProbe.ShouldRetryWithGet(notFoundHead), "HEAD 404 must not retry with GET.");
        }

        private static void CalendarAvailabilityTransientOverrideUnlocksTodayOnly()
        {
            var yesterday = new DateTime(2026, 7, 14);
            var today = new DateTime(2026, 7, 15);
            var tomorrow = new DateTime(2026, 7, 16);

            var effectiveLatest = apod_wallpaper.ApodCalendarAvailability.ResolveEffectiveLatestPublishedDate(yesterday, today);
            Assert(effectiveLatest == today, "Expected transient available date to advance effective latest published date.");
            Assert(tomorrow > effectiveLatest, "Expected tomorrow to remain future.");
        }

        private static void CalendarAvailabilityThrottleResetsWhenTodayChanges()
        {
            var previousDay = new DateTime(2026, 7, 14);
            var currentDay = new DateTime(2026, 7, 15);
            var lastProbeUtc = new DateTime(2026, 7, 14, 20, 59, 0, DateTimeKind.Utc);
            var nowUtc = new DateTime(2026, 7, 14, 21, 1, 0, DateTimeKind.Utc);
            var throttle = TimeSpan.FromMinutes(5);

            Assert(!apod_wallpaper.ApodCalendarAvailability.ShouldThrottleProbe(currentDay, previousDay, lastProbeUtc, nowUtc, throttle),
                "Expected new APOD date to bypass throttle.");
            Assert(apod_wallpaper.ApodCalendarAvailability.ShouldThrottleProbe(previousDay, previousDay, lastProbeUtc, nowUtc, throttle),
                "Expected same APOD date to remain throttled within the throttle window.");
            Assert(!apod_wallpaper.ApodCalendarAvailability.ShouldThrottleProbe(previousDay, null, lastProbeUtc, nowUtc, throttle),
                "Expected missing last probe date to skip throttle.");
        }

        private static void ApodAvailabilityProbeSourceAvoidsWorkflowSideEffects()
        {
            var probeSourcePath = Path.Combine(GetRepositoryRoot(), "apod_wallpaper.Core", "ApodPageAvailabilityProbe.cs");
            var source = File.ReadAllText(probeSourcePath);
            var forbiddenTokens = new[]
            {
                "ApplyDay",
                "ApplyLatest",
                "DownloadDay",
                "LoadDay",
                "ApodWorkflowService",
                "Scheduler",
            };

            foreach (var token in forbiddenTokens)
            {
                Assert(!source.Contains(token), "Availability probe must not call workflow side-effect path: " + token);
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

        private static void HtmlExtractorResolvesTextMetadata()
        {
            const string html =
@"<html>
<head>
<title>APOD: 2026 May 04 - Spiral Echoes</title>
</head>
<body>
<center>
2026 May 04
<br>
<a href=""image/2605/spiral_full.jpg"">
<img src=""image/2605/spiral_preview.jpg""></a>
</center>
<p><b> Explanation: </b>
Spiral dust lanes &amp; glowing gas reveal how galaxies evolve.
<p>
Bright clusters mark newborn stars.
</p>
<p> <center>
<b> Growing Gallery: </b><a href=""ap260503.html"">extra related link</a>
</center>
<p>Tomorrow's picture: another sky surprise.</p>
</body>
</html>";

            var title = apod_wallpaper.ApodPageImageExtractor.ExtractTitle(html);
            var explanation = apod_wallpaper.ApodPageImageExtractor.ExtractExplanation(html);

            Assert(title == "Spiral Echoes", "Expected APOD HTML title to be extracted.");
            Assert(explanation == "Spiral dust lanes & glowing gas reveal how galaxies evolve. Bright clusters mark newborn stars.",
                "Expected APOD explanation text to be extracted and normalized.");
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
                Assert(result.LatestPublishedDate == today, "Expected ApplyLatestPublished to preserve the real latest published date for calendar updates.");
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
                var screenBounds = apod_wallpaper.DisplayMetrics.GetPrimaryScreenBounds();
                if (screenBounds.Width <= 0 || screenBounds.Height <= 0)
                    screenBounds = new Rectangle(0, 0, 1920, 1080);
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

        private static void SmartComposerPreservesUltraWideImages()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_smoke_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            var snapshot = CaptureSettings();

            try
            {
                var imagePath = Path.Combine(tempDirectory, "ultrawide.jpg");

                using (var bitmap = new Bitmap(2920, 1000))
                {
                    bitmap.Save(imagePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                apod_wallpaper.FileStorage.SetSessionImagesDirectory(tempDirectory);
                var composition = apod_wallpaper.SmartWallpaperComposer.Prepare(imagePath);

                Assert(composition.Style == apod_wallpaper.WallpaperStyle.Fill, "Expected ultrawide image to use Fill only after smart composition.");
                Assert(composition.Strategy == "wide_focus_background", "Expected ultrawide image to use wide focus strategy instead of raw Fill cropping.");
                Assert(File.Exists(composition.ImagePath), "Expected composed ultrawide smart wallpaper file to exist.");
                Assert(!string.Equals(composition.ImagePath, imagePath, StringComparison.OrdinalIgnoreCase), "Expected ultrawide smart mode to create a composed wallpaper rather than applying the original image.");
            }
            finally
            {
                RestoreSettings(snapshot);
                TryDeleteDirectory(tempDirectory);
            }
        }

        private static void SchedulerUsesHourlyPollingForDemoKey()
        {
            var pollingInterval = apod_wallpaper.ApplicationController.ResolveSchedulerPollingInterval(new apod_wallpaper.ApplicationSettingsSnapshot
            {
                AutoRefreshEnabled = true,
                NasaApiKey = "DEMO_KEY",
            });

            Assert(pollingInterval == TimeSpan.FromHours(1), "Expected DEMO_KEY polling interval to be one hour.");
        }

        private static void SchedulerUsesThirtyMinutePollingForPersonalKey()
        {
            var pollingInterval = apod_wallpaper.ApplicationController.ResolveSchedulerPollingInterval(new apod_wallpaper.ApplicationSettingsSnapshot
            {
                AutoRefreshEnabled = true,
                NasaApiKey = "personal-key",
                NasaApiKeyValidationState = apod_wallpaper.ApiKeyValidationState.Valid.ToString(),
            });

            Assert(pollingInterval == TimeSpan.FromMinutes(30), "Expected personal key polling interval to be 30 minutes.");
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

        private static void SchedulerDayLockSkipsAfterTodaysImage()
        {
            var today = DateTime.Today;
            var shouldSkip = apod_wallpaper.ApplicationController.ShouldSkipSchedulerForToday(today, today, today);
            Assert(shouldSkip, "Expected scheduler day lock to skip repeated checks after today's image was applied.");
        }

        private static void SchedulerDayLockKeepsCheckingAfterYesterdayFallback()
        {
            var today = DateTime.Today;
            var shouldSkip = apod_wallpaper.ApplicationController.ShouldSkipSchedulerForToday(today, today.AddDays(-1), today);
            Assert(!shouldSkip, "Expected scheduler day lock to keep checking when today's early run only applied an older fallback image.");
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
                Assert(_secretStore.GetNasaApiKey() == "protected-test-key", "Expected API key to be stored in protected storage.");
                Assert(GetValueOrThrow(controller.GetSettingsAsync().GetAwaiter().GetResult(), "Unable to read settings after protected save.").NasaApiKey == "protected-test-key", "Expected facade settings to surface the protected API key.");
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
                var store = new apod_wallpaper.JsonSettingsStore(settingsPath);
                var snapshot = new apod_wallpaper.ApplicationSettingsSnapshot
                {
                    TrayDoubleClickAction = true,
                    WallpaperStyleIndex = (int)apod_wallpaper.WallpaperStyle.Fill,
                    AutoRefreshEnabled = true,
                    StartWithWindows = false,
                    NasaApiKeyValidationState = apod_wallpaper.ApiKeyValidationState.Valid.ToString(),
                    ImagesDirectoryPath = @"C:\temp\images",
                    TranslationTargetLanguage = apod_wallpaper.TranslationTargetLanguage.Russian,
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
                Assert(loaded.TranslationTargetLanguage == apod_wallpaper.TranslationTargetLanguage.Russian, "Expected translation target language to round-trip through settings.json.");
            }
            finally
            {
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

        private static void StorageSummaryCountsLocalLibraryWithoutCleanup()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_storage_summary_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            Directory.CreateDirectory(Path.Combine(tempDirectory, "smart"));
            var snapshot = CaptureSettings();
            try
            {
                using (var bitmap = new Bitmap(4, 4))
                {
                    bitmap.Save(Path.Combine(tempDirectory, "2026-07-14.jpg"), System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                File.WriteAllText(Path.Combine(tempDirectory, "smart", "2026-07-14-smart.txt"), "generated");
                apod_wallpaper.FileStorage.SetSessionImagesDirectory(tempDirectory);

                var summary = new apod_wallpaper.StorageSummaryService().GetStorageSummary();
                Assert(summary.DownloadedImageCount == 1, "Expected one valid downloaded image.");
                Assert(summary.DownloadedImageSizeBytes > 0, "Expected downloaded image size to be counted.");
                Assert(summary.SmartImages.FileCount == 1, "Expected smart variant files to be counted separately.");
                Assert(File.Exists(Path.Combine(tempDirectory, "2026-07-14.jpg")), "Storage summary must not delete original images.");
                Assert(File.Exists(Path.Combine(tempDirectory, "smart", "2026-07-14-smart.txt")), "Storage summary must not delete generated variants.");
            }
            finally
            {
                apod_wallpaper.FileStorage.SetSessionImagesDirectory(snapshot.ImagesDirectoryPath);
                TryDeleteDirectory(tempDirectory);
            }
        }

        private static void PortableStorageModeUsesPortableLayout()
        {
            apod_wallpaper.ApplicationStorageLayout.Configure(apod_wallpaper.ApplicationStorageMode.Portable);
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
                apod_wallpaper.ApplicationStorageLayout.ResetConfiguration();
            }
        }

        private static void StoreStorageModeUsesSandboxLayout()
        {
            var sandboxPath = Path.Combine(Path.GetTempPath(), "apod_wallpaper_store_layout_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sandboxPath);

            try
            {
                apod_wallpaper.ApplicationStorageLayout.Configure(apod_wallpaper.ApplicationStorageMode.Store, sandboxPath);
                apod_wallpaper.FileStorage.SetSessionImagesDirectory(null);
                apod_wallpaper.AppRuntimeSettings.Configure(null, null, apod_wallpaper.ApiKeyValidationState.Unknown);

                var controller = CreateController();
                var layout = GetValueOrThrow(controller.EnsureStorageLayoutAsync().GetAwaiter().GetResult(), "Unable to prepare store storage layout.");

                Assert(layout.Mode == apod_wallpaper.ApplicationStorageMode.Store, "Expected store storage mode.");
                Assert(string.Equals(layout.ApplicationDataDirectory, sandboxPath, StringComparison.OrdinalIgnoreCase), "Expected store application data directory to use the sandbox path.");
                Assert(string.Equals(layout.ImagesDirectory, Path.Combine(sandboxPath, "images"), StringComparison.OrdinalIgnoreCase), "Expected store images directory inside sandbox path.");
                Assert(string.Equals(layout.SmartImagesDirectory, Path.Combine(sandboxPath, "images", "smart"), StringComparison.OrdinalIgnoreCase), "Expected store smart images directory inside sandbox path.");
                Assert(string.Equals(layout.CacheDirectory, Path.Combine(sandboxPath, "cache"), StringComparison.OrdinalIgnoreCase), "Expected store cache directory inside sandbox path.");
                Assert(string.Equals(layout.LogsDirectory, Path.Combine(sandboxPath, "logs"), StringComparison.OrdinalIgnoreCase), "Expected store logs directory inside sandbox path.");
                Assert(string.Equals(layout.SecretsDirectory, Path.Combine(sandboxPath, "secrets"), StringComparison.OrdinalIgnoreCase), "Expected store secrets directory inside sandbox path.");
                Assert(string.Equals(layout.SettingsFilePath, Path.Combine(sandboxPath, "settings.json"), StringComparison.OrdinalIgnoreCase), "Expected store settings.json path inside sandbox path.");

                var settingsStore = new apod_wallpaper.JsonSettingsStore();
                var storeSecret = new apod_wallpaper.DpapiUserSecretStore();
                settingsStore.Save(CreateDefaultSettingsSnapshot());
                storeSecret.SaveNasaApiKey("sandbox-key");

                Assert(File.Exists(Path.Combine(sandboxPath, "settings.json")), "Expected settings.json to be written into the sandbox path.");
                Assert(File.Exists(Path.Combine(sandboxPath, "secrets", "nasa-api-key.bin")), "Expected protected secret file to be written into the sandbox path.");
                Assert(storeSecret.GetNasaApiKey() == "sandbox-key", "Expected protected secret round-trip in store storage mode.");
            }
            finally
            {
                apod_wallpaper.ApplicationStorageLayout.ResetConfiguration();
                TryDeleteDirectory(sandboxPath);
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

        private static void WinUiLocalizationLiteralsAreCovered()
        {
            const string AutoOnRussian = "\u0410\u0432\u0442\u043e \u0432\u043a\u043b.";
            const string AutoOffRussian = "\u0410\u0432\u0442\u043e \u0432\u044b\u043a\u043b.";

            var repoRoot = ResolveRepositoryRoot();
            var winUiDirectory = Path.Combine(repoRoot, "apod_wallpaper.WinUI");
            var appStringsPath = Path.Combine(winUiDirectory, "AppStrings.cs");
            var appStringsSource = File.ReadAllText(appStringsPath);
            var appStringKeys = ExtractAppStringKeys(appStringsSource);

            Assert(!appStringsSource.Contains("LanguageSystem"), "AppStrings must not use the removed System language.");
            Assert(!appStringsSource.Contains("CultureInfo.CurrentUICulture.TwoLetterISOLanguageName"), "AppStrings must not choose UI language from CurrentUICulture.");
            Assert(appStringKeys.Contains("CopyFailed"), "AppStrings must contain CopyFailed.");
            Assert(appStringKeys.Contains("Translation language"), "AppStrings must contain the translation language tooltip prefix.");
            foreach (var languageName in new[] { "Russian", "Spanish", "German", "French", "Italian", "Portuguese", "Japanese" })
                Assert(appStringKeys.Contains(languageName), "AppStrings must contain language name: " + languageName);

            foreach (var xamlPath in Directory.GetFiles(winUiDirectory, "*.xaml"))
                AssertXamlLiteralsHaveKeys(xamlPath, appStringKeys);

            var mainPageSource = File.ReadAllText(Path.Combine(winUiDirectory, "MainPage.xaml.cs"));
            Assert(!mainPageSource.Contains("TranslationTargetDisplay"), "Translation target display must not use internal localization keys.");
            Assert(!mainPageSource.Contains("TranslationTargetPlaceholder"), "Translation target placeholder key must not appear in MainPage UI code.");

            foreach (var path in Directory.GetFiles(winUiDirectory, "*.cs").Concat(Directory.GetFiles(winUiDirectory, "*.xaml")))
            {
                if (string.Equals(Path.GetFileName(path), "AppStrings.cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                var text = File.ReadAllText(path);
                if (text.Contains(AutoOnRussian) || text.Contains(AutoOffRussian))
                    throw new InvalidOperationException("Auto labels must only appear in AppStrings: " + path);

                if (ContainsCyrillic(text) && !IsAllowedCyrillicFile(path, text))
                    throw new InvalidOperationException("Russian UI text must live in AppStrings: " + path);

                if (text.Contains("Content=\"System\"") || text.Contains("Tag=\"system\"") || text.Contains("LanguageSystem"))
                    throw new InvalidOperationException("System language leftover found in UI file: " + path);

                if (text.Contains("CultureInfo.CurrentUICulture.TwoLetterISOLanguageName"))
                    throw new InvalidOperationException("CurrentUICulture is used as UI language source in: " + path);

                AssertNoDirectUserVisibleAssignments(path, text);
            }
        }

        private static void TranslationTargetLanguageNormalizesValues()
        {
            Assert(apod_wallpaper.TranslationTargetLanguage.Normalize(null) == apod_wallpaper.TranslationTargetLanguage.Russian, "Expected null target language to normalize to ru.");
            Assert(apod_wallpaper.TranslationTargetLanguage.Normalize(string.Empty) == apod_wallpaper.TranslationTargetLanguage.Russian, "Expected empty target language to normalize to ru.");
            Assert(apod_wallpaper.TranslationTargetLanguage.Normalize(" RU ") == apod_wallpaper.TranslationTargetLanguage.Russian, "Expected RU to normalize to ru.");
            Assert(apod_wallpaper.TranslationTargetLanguage.Normalize("es") == apod_wallpaper.TranslationTargetLanguage.Spanish, "Expected es to remain valid.");
            Assert(apod_wallpaper.TranslationTargetLanguage.Normalize("en") == apod_wallpaper.TranslationTargetLanguage.Russian, "Expected English target language to normalize to ru.");
            Assert(apod_wallpaper.TranslationTargetLanguage.Normalize("unknown") == apod_wallpaper.TranslationTargetLanguage.Russian, "Expected unknown target language to normalize to ru.");
            Assert(apod_wallpaper.TranslationTargetLanguage.GetDisplayCode("ru") == "ru", "Expected ru display code.");
            Assert(apod_wallpaper.TranslationTargetLanguage.GetDisplayCode("es") == "es", "Expected es display code.");
            Assert(apod_wallpaper.TranslationTargetLanguage.GetDisplayCode("ja") == "ja", "Expected ja display code.");
        }

        private static void GoogleTranslateUrlBuilderEncodesExplanation()
        {
            var text = "Stars & \"dust\"" + Environment.NewLine + "Unicode: Привет";
            var url = apod_wallpaper.TranslationTargetLanguage.BuildGoogleTranslateUrl(
                apod_wallpaper.TranslationTargetLanguage.Russian,
                text,
                includeText: true);

            Assert(url.StartsWith("https://translate.google.com/?sl=en&tl=ru&text=", StringComparison.Ordinal), "Expected Google Translate URL to use en source and ru target.");
            Assert(url.EndsWith("&op=translate", StringComparison.Ordinal), "Expected Google Translate URL to use translate mode.");
            Assert(url.Contains("Stars%20%26%20%22dust%22"), "Expected spaces, ampersands, and quotes to be URL encoded.");
            Assert(url.Contains("%D0%9F%D1%80%D0%B8%D0%B2%D0%B5%D1%82"), "Expected Unicode text to be URL encoded.");

            var urlWithoutText = apod_wallpaper.TranslationTargetLanguage.BuildGoogleTranslateUrl(
                apod_wallpaper.TranslationTargetLanguage.Japanese,
                text,
                includeText: false);
            Assert(urlWithoutText == "https://translate.google.com/?sl=en&tl=ja&op=translate", "Expected fallback URL without text payload.");
        }

        private static string ResolveRepositoryRoot()
        {
            foreach (var start in new[] { Environment.CurrentDirectory, AppDomain.CurrentDomain.BaseDirectory })
            {
                var directory = new DirectoryInfo(start);
                while (directory != null)
                {
                    var appStringsPath = Path.Combine(directory.FullName, "apod_wallpaper.WinUI", "AppStrings.cs");
                    if (File.Exists(appStringsPath))
                        return directory.FullName;

                    directory = directory.Parent;
                }
            }

            throw new InvalidOperationException("Unable to locate repository root for localization audit.");
        }

        private static HashSet<string> ExtractAppStringKeys(string source)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(source, "\\[\"(?<key>(?:\\\\.|[^\"\\\\])*)\"\\]\\s*=\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\""))
            {
                keys.Add(match.Groups["key"].Value);
                keys.Add(match.Groups["value"].Value);
            }

            Assert(keys.Contains("Auto On") && keys.Contains("\u0410\u0432\u0442\u043e \u0432\u043a\u043b."), "Expected Auto On localization pair.");
            Assert(keys.Contains("Local") && keys.Contains("\u041b\u043e\u043a\u0430\u043b\u044c\u043d\u043e"), "Expected calendar legend localization pair.");
            Assert(keys.Contains("On") && keys.Contains("\u0412\u043a\u043b."), "Expected ToggleSwitch On localization pair.");
            return keys;
        }

        private static void AssertXamlLiteralsHaveKeys(string path, HashSet<string> appStringKeys)
        {
            var text = File.ReadAllText(path);
            var pattern = "(Text|Content|Header|Label|PlaceholderText|ToolTipService\\.ToolTip|AutomationProperties\\.Name|AutomationProperties\\.HelpText|OnContent|OffContent)\\s*=\\s*\"(?<value>[^\"]*)\"";
            foreach (Match match in Regex.Matches(text, pattern))
            {
                var value = match.Groups["value"].Value;
                if (ShouldIgnoreXamlLiteral(value))
                    continue;

                if (!appStringKeys.Contains(value))
                    throw new InvalidOperationException(Path.GetFileName(path) + " literal is missing from AppStrings: " + value);
            }
        }

        private static bool ShouldIgnoreXamlLiteral(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;
            if (value.StartsWith("{", StringComparison.Ordinal))
                return true;
            if (!Regex.IsMatch(value, "[A-Za-z\u0400-\u04FF]"))
                return true;

            return value == "English" || value == "\u0420\u0443\u0441\u0441\u043a\u0438\u0439";
        }

        private static bool ContainsCyrillic(string text)
        {
            return Regex.IsMatch(text, "[\u0400-\u04FF]");
        }

        private static bool IsAllowedCyrillicFile(string path, string text)
        {
            return string.Equals(Path.GetFileName(path), "SettingsPage.xaml", StringComparison.OrdinalIgnoreCase)
                && text.Contains("Content=\"\u0420\u0443\u0441\u0441\u043a\u0438\u0439\"");
        }

        private static void AssertNoDirectUserVisibleAssignments(string path, string text)
        {
            var pattern = "\\.(Text|Content|Title|Message|Header|Label)\\s*=\\s*\"(?<value>[^\"]*[A-Za-z\u0400-\u04FF][^\"]*)\"";
            foreach (Match match in Regex.Matches(text, pattern))
            {
                var value = match.Groups["value"].Value;
                if (value.StartsWith("http", StringComparison.OrdinalIgnoreCase) || value.Contains("://"))
                    continue;

                throw new InvalidOperationException(Path.GetFileName(path) + " assigns user-visible text without AppStrings: " + value);
            }
        }

        private static void RestoreSettings(apod_wallpaper.ApplicationSettingsSnapshot snapshot)
        {
            var restoreResult = CreateController().SaveSettingsAsync(snapshot).GetAwaiter().GetResult();
            Assert(restoreResult.Succeeded, "Expected settings restore to succeed.");
        }

        private static apod_wallpaper.ApplicationController CreateController()
        {
            return new apod_wallpaper.ApplicationController(
                _settingsStore,
                _secretStore,
                new FakeStartupRegistrationService());
        }

        private static apod_wallpaper.ApplicationSettingsSnapshot CreateDefaultSettingsSnapshot()
        {
            return new apod_wallpaper.ApplicationSettingsSnapshot
            {
                TrayDoubleClickAction = false,
                WallpaperStyleIndex = (int)apod_wallpaper.WallpaperStyle.Smart,
                AutoRefreshEnabled = false,
                StartWithWindows = true,
                NasaApiKey = "DEMO_KEY",
                NasaApiKeyValidationState = apod_wallpaper.ApiKeyValidationState.Unknown.ToString(),
                ImagesDirectoryPath = string.Empty,
                TranslationTargetLanguage = apod_wallpaper.TranslationTargetLanguage.Russian,
                LastAutoRefreshRunDate = string.Empty,
                LastAutoRefreshAppliedDate = string.Empty,
            };
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

        private static string GetRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "apod_wallpaper.sln")))
                    return directory.FullName;

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Unable to locate repository root from " + AppContext.BaseDirectory);
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

        private sealed class InMemorySettingsStore : apod_wallpaper.IApplicationSettingsStore
        {
            private apod_wallpaper.ApplicationSettingsSnapshot _snapshot;

            public InMemorySettingsStore(apod_wallpaper.ApplicationSettingsSnapshot initialSnapshot)
            {
                _snapshot = (initialSnapshot ?? new apod_wallpaper.ApplicationSettingsSnapshot()).Clone();
            }

            public bool Exists()
            {
                return _snapshot != null;
            }

            public apod_wallpaper.ApplicationSettingsSnapshot Load()
            {
                return (_snapshot ?? new apod_wallpaper.ApplicationSettingsSnapshot()).Clone();
            }

            public void Save(apod_wallpaper.ApplicationSettingsSnapshot settings)
            {
                _snapshot = (settings ?? new apod_wallpaper.ApplicationSettingsSnapshot()).Clone();
            }
        }

        private sealed class FakeStartupRegistrationService : apod_wallpaper.IStartupRegistrationService
        {
            public void SetStartWithWindows(bool enabled)
            {
            }
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

            public string ReapplyCurrentWallpaperStyle(apod_wallpaper.WallpaperStyle style)
            {
                LastAppliedStyle = style;
                return LastAppliedImagePath;
            }

            public string ResolveCurrentWallpaperSourcePath()
            {
                return LastAppliedImagePath;
            }
        }
    }
}
