using System;
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
