using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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

        private static apod_wallpaper.ApplicationSettingsSnapshot CaptureSettings()
        {
            return new apod_wallpaper.ApplicationController().GetSettings();
        }

        private static void RestoreSettings(apod_wallpaper.ApplicationSettingsSnapshot snapshot)
        {
            new apod_wallpaper.ApplicationController().SaveSettings(snapshot);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
