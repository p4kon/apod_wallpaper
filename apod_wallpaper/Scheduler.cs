using System;
using System.Threading;

namespace apod_wallpaper
{
    public sealed class Scheduler : IDisposable
    {
        private readonly object _syncRoot = new object();
        private Timer _timer;
        private Action _scheduledTask;
        private TimeSpan _scheduledTime;

        public int EveryHour { get; set; }
        public int EveryMinute { get; set; }
        public int EverySecond { get; set; }

        public bool IsRunning { get; private set; }
        public DateTime? NextRun { get; private set; }

        public void Start(Action scheduledTask)
        {
            if (scheduledTask == null)
                throw new ArgumentNullException(nameof(scheduledTask));

            lock (_syncRoot)
            {
                _scheduledTask = scheduledTask;
                _scheduledTime = new TimeSpan(EveryHour, EveryMinute, EverySecond);
                IsRunning = true;
                ScheduleNextRun();
            }
        }

        public void UpdateSchedule()
        {
            lock (_syncRoot)
            {
                _scheduledTime = new TimeSpan(EveryHour, EveryMinute, EverySecond);
                if (IsRunning)
                    ScheduleNextRun();
            }
        }

        public void Stop()
        {
            lock (_syncRoot)
            {
                IsRunning = false;
                NextRun = null;
                DisposeTimer();
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void ScheduleNextRun()
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

        private void RunScheduledTask(object state)
        {
            Action task;

            lock (_syncRoot)
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
                lock (_syncRoot)
                {
                    if (IsRunning)
                        ScheduleNextRun();
                }
            }
        }

        private void DisposeTimer()
        {
            if (_timer == null)
                return;

            _timer.Dispose();
            _timer = null;
        }
    }
}
