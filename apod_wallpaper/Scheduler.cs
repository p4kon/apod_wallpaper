using System;
using System.Threading;

namespace apod_wallpaper
{
    public static class Scheduler
    {
        private static readonly object SyncRoot = new object();
        private static Timer _timer;
        private static Action _scheduledTask;
        private static TimeSpan _scheduledTime;

        public static int EveryHour { get; set; }
        public static int EveryMinute { get; set; }
        public static int EverySecond { get; set; }

        public static bool IsRunning { get; private set; }
        public static DateTime? NextRun { get; private set; }

        public static void Start(Action scheduledTask)
        {
            if (scheduledTask == null)
                throw new ArgumentNullException(nameof(scheduledTask));

            lock (SyncRoot)
            {
                _scheduledTask = scheduledTask;
                _scheduledTime = new TimeSpan(EveryHour, EveryMinute, EverySecond);
                IsRunning = true;
                ScheduleNextRun();
            }
        }

        public static void UpdateSchedule()
        {
            lock (SyncRoot)
            {
                _scheduledTime = new TimeSpan(EveryHour, EveryMinute, EverySecond);
                if (IsRunning)
                    ScheduleNextRun();
            }
        }

        public static void Stop()
        {
            lock (SyncRoot)
            {
                IsRunning = false;
                NextRun = null;
                if (_timer != null)
                {
                    _timer.Dispose();
                    _timer = null;
                }
            }
        }

        private static void ScheduleNextRun()
        {
            var now = DateTime.Now;
            var nextRun = now.Date.Add(_scheduledTime);
            if (nextRun <= now)
                nextRun = nextRun.AddDays(1);

            NextRun = nextRun;

            var dueTime = nextRun - now;
            if (_timer == null)
            {
                _timer = new Timer(RunScheduledTask, null, dueTime, Timeout.InfiniteTimeSpan);
            }
            else
            {
                _timer.Change(dueTime, Timeout.InfiniteTimeSpan);
            }
        }

        private static void RunScheduledTask(object state)
        {
            Action task;

            lock (SyncRoot)
            {
                if (!IsRunning)
                    return;

                task = _scheduledTask;
            }

            try
            {
                task();
            }
            finally
            {
                lock (SyncRoot)
                {
                    if (IsRunning)
                        ScheduleNextRun();
                }
            }
        }
    }
}
