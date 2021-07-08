using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    public static class Scheduler
    {
        private static int executedToday;
        private static bool autoExecute;
        private static int _everyHour;
        private static int _everyMinute;
        private static int _everySecond;

        public static int EveryHour
        {
            get 
            {
                return _everyHour;
            }

            set
            {
                _everyHour = value;
            }
        }

        public static int EverySecond
        {
            get
            {
                return _everySecond;
            }

            set
            {
                _everySecond = value;
            }
        }

        public static int EveryMinute
        {
            get
            {
                return _everyMinute;
            }

            set
            {
                _everyMinute = value;
            }
        }

        public async static void Check(params Action[] tasks)
        {
            DateTime runTime = DateTime.Now./*AddHours((double)_everyHour).AddMinutes((double)_everyMinute).*/AddSeconds(/*(double)_everySecond*/10.0);
            TimeSpan ts = new TimeSpan(runTime.Hour, runTime.Minute, runTime.Second); //(runTime.Hour, 0, 0);
            runTime = runTime.Date + ts;

            Console.WriteLine("next run will be at: {0} and current hour is: {1}", runTime, DateTime.Now);
            while (true)
            {
                TimeSpan duration = runTime.Subtract(DateTime.Now);
                if (duration.TotalMilliseconds <= 0.0)
                {
                    Parallel.Invoke(tasks);
                    Console.WriteLine("It is the run time as shown before to be: {0} confirmed with system time, that is: {1}", runTime, DateTime.Now);
                    runTime = DateTime.Now/*.AddHours((double)_everyHour).AddMinutes((double)_everyMinute)*/.AddSeconds(/*(double)_everySecond*/10.0);
                    Console.WriteLine("next run will be at: {0} and current hour is: {1}", runTime, DateTime.Now);
                    continue;
                }
                int delay = (int)(duration.TotalMilliseconds / 2);
                await Task.Delay(5000);  // 3 seconds
            }
        }
    }
}
