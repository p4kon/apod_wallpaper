using System;

namespace apod_wallpaper
{
    public sealed class ApodDayAvailability
    {
        public DateTime Date { get; set; }
        public bool IsKnown { get; set; }
        public bool HasImage { get; set; }
        public string MediaType { get; set; }
    }
}
