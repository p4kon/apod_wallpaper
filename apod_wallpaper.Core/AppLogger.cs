using System;
using System.IO;
using System.Text;

namespace apod_wallpaper
{
    internal static class AppLogger
    {
        private static readonly object SyncRoot = new object();
        private static string _logDirectoryOverride;

        public static void Info(string message)
        {
            Write("INFO", message, null);
        }

        public static void Warn(string message, Exception exception = null)
        {
            Write("WARN", message, exception);
        }

        public static void Error(string message, Exception exception = null)
        {
            Write("ERROR", message, exception);
        }

        public static void Web(string message)
        {
            Write("WEB", message, null);
        }

        internal static void SetLogDirectoryOverride(string path)
        {
            _logDirectoryOverride = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        }

        internal static void ClearLogDirectoryOverride()
        {
            _logDirectoryOverride = null;
        }

        private static void Write(string level, string message, Exception exception)
        {
            try
            {
                var logsDirectory = _logDirectoryOverride;
                if (string.IsNullOrWhiteSpace(logsDirectory))
                {
                    FileStorage.EnsureLogsDirectory();
                    logsDirectory = FileStorage.LogsDirectory;
                }
                else
                {
                    Directory.CreateDirectory(logsDirectory);
                }

                var logFilePath = Path.Combine(
                    logsDirectory,
                    "apod_wallpaper_" + DateTime.Now.ToString("yyyy-MM-dd") + ".log");

                var builder = new StringBuilder();
                builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                builder.Append(" [");
                builder.Append(level);
                builder.Append("] ");
                builder.AppendLine(message);

                if (exception != null)
                {
                    builder.AppendLine(exception.GetType().FullName + ": " + exception.Message);
                    builder.AppendLine(exception.StackTrace);
                }

                lock (SyncRoot)
                {
                    File.AppendAllText(logFilePath, builder.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
            }
        }
    }
}
