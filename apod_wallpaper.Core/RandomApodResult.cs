using System;

namespace apod_wallpaper
{
    public sealed class RandomApodResult
    {
        public RandomApodStatus Status { get; set; }
        public DateTime? Date { get; set; }
        public string Source { get; set; }
        public bool IncludeDeepArchive { get; set; }
        public int Attempts { get; set; }
        public string Message { get; set; }

        public static RandomApodResult Success(DateTime date, string source, bool includeDeepArchive, int attempts)
        {
            return new RandomApodResult
            {
                Status = RandomApodStatus.Success,
                Date = date.Date,
                Source = RandomApodSource.Normalize(source),
                IncludeDeepArchive = includeDeepArchive,
                Attempts = attempts,
                Message = string.Empty,
            };
        }

        public static RandomApodResult NoCandidates(string source, bool includeDeepArchive, string message)
        {
            return new RandomApodResult
            {
                Status = RandomApodStatus.NoCandidates,
                Source = RandomApodSource.Normalize(source),
                IncludeDeepArchive = includeDeepArchive,
                Attempts = 0,
                Message = message ?? string.Empty,
            };
        }

        public static RandomApodResult Unavailable(string source, bool includeDeepArchive, int attempts, string message)
        {
            return new RandomApodResult
            {
                Status = RandomApodStatus.Unavailable,
                Source = RandomApodSource.Normalize(source),
                IncludeDeepArchive = includeDeepArchive,
                Attempts = attempts,
                Message = message ?? string.Empty,
            };
        }
    }
}
