using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace apod_wallpaper.SmokeTests
{
    internal static class Program
    {
        private static int _failures;

        [STAThread]
        private static int Main()
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

            Console.WriteLine(_failures == 0
                ? "Smoke tests passed."
                : "Smoke tests failed: " + _failures);

            return _failures == 0 ? 0 : 1;
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
                var controller = new apod_wallpaper.ApplicationController();
                controller.SaveSettings(new apod_wallpaper.ApplicationSettingsSnapshot
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
                });

                controller.SaveSettings(new apod_wallpaper.ApplicationSettingsSnapshot
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
                });

                Assert(controller.GetApiKeyValidationState() == apod_wallpaper.ApiKeyValidationState.Unknown,
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
                var expectedDate = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");
                apod_wallpaper.Properties.Settings.Default.LastAutoRefreshAppliedDate = expectedDate;
                apod_wallpaper.Properties.Settings.Default.Save();

                var controller = new apod_wallpaper.ApplicationController();
                Assert(controller.GetPreferredDisplayDate() == DateTime.Today.AddDays(-1),
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
                controller = new apod_wallpaper.ApplicationController();
                controller.SaveSettings(new apod_wallpaper.ApplicationSettingsSnapshot
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
                });

                controller.Initialize();
                Assert(controller.Scheduler.PollingInterval == TimeSpan.FromHours(1), "Expected DEMO_KEY polling interval to be one hour.");
            }
            finally
            {
                if (controller != null)
                    controller.Dispose();
                RestoreSettings(snapshot);
            }
        }

        private static void SchedulerUsesThirtyMinutePollingForPersonalKey()
        {
            var snapshot = CaptureSettings();
            apod_wallpaper.ApplicationController controller = null;
            try
            {
                controller = new apod_wallpaper.ApplicationController();
                controller.SaveSettings(new apod_wallpaper.ApplicationSettingsSnapshot
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
                });

                controller.Initialize();
                Assert(controller.Scheduler.PollingInterval == TimeSpan.FromMinutes(30), "Expected personal key polling interval to be 30 minutes.");
            }
            finally
            {
                if (controller != null)
                    controller.Dispose();
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

        private static apod_wallpaper.ApplicationSettingsSnapshot CaptureSettings()
        {
            return new apod_wallpaper.ApplicationController().GetSettings();
        }

        private static void RestoreSettings(apod_wallpaper.ApplicationSettingsSnapshot snapshot)
        {
            new apod_wallpaper.ApplicationController().SaveSettings(snapshot);
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
