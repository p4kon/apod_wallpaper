using System;
using System.Threading;

namespace apod_wallpaper
{
    public sealed class Scheduler : IDisposable
    {
        private readonly object _syncRoot = new object();
        private static readonly TimeSpan DefaultPollingInterval = TimeSpan.FromMinutes(5);
        private Timer _timer;
        private Action _scheduledTask;
        private int _taskIsRunning;

        public bool IsRunning { get; private set; }
        public DateTime? NextRun { get; private set; }
        public TimeSpan PollingInterval { get; set; } = DefaultPollingInterval;

        public void Start(Action scheduledTask)
        {
            if (scheduledTask == null)
                throw new ArgumentNullException(nameof(scheduledTask));

            lock (_syncRoot)
            {
                _scheduledTask = scheduledTask;
                IsRunning = true;
                SchedulePolling();
            }
        }

        public void UpdateSchedule()
        {
            lock (_syncRoot)
            {
                if (IsRunning)
                    SchedulePolling();
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

        private void SchedulePolling()
        {
            var dueTime = TimeSpan.Zero;
            var period = PollingInterval <= TimeSpan.Zero ? DefaultPollingInterval : PollingInterval;
            NextRun = DateTime.Now.Add(period);
            if (_timer == null)
            {
                _timer = new Timer(RunScheduledTask, null, dueTime, period);
            }
            else
            {
                _timer.Change(dueTime, period);
            }
        }

        private void RunScheduledTask(object state)
        {
            if (Interlocked.Exchange(ref _taskIsRunning, 1) == 1)
                return;

            Action task = null;

            lock (_syncRoot)
            {
                if (IsRunning)
                    task = _scheduledTask;
            }

            try
            {
                if (task != null)
                    task();
            }
            finally
            {
                Interlocked.Exchange(ref _taskIsRunning, 0);
                lock (_syncRoot)
                {
                    if (IsRunning)
                        NextRun = DateTime.Now.Add(PollingInterval <= TimeSpan.Zero ? DefaultPollingInterval : PollingInterval);
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
