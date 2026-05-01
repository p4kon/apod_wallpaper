using System;

namespace apod_wallpaper
{
    internal sealed class ApodEntryUnavailableException : Exception
    {
        public ApodEntryUnavailableException(DateTime requestedDate, string message)
            : base(message)
        {
            RequestedDate = requestedDate.Date;
        }

        public DateTime RequestedDate { get; }
    }
}
