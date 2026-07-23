using System;

namespace apod_wallpaper
{
    public sealed class FavoriteApodItem
    {
        public DateTime Date { get; set; }
        public string ImagePath { get; set; }
        public string Title { get; set; }
    }
}
