using System;
using System.Runtime.Serialization;

namespace apod_wallpaper
{
    [DataContract]
    internal sealed class ApodCachedEntry
    {
        [DataMember(Order = 1)]
        public string Date { get; set; }

        [DataMember(Order = 2)]
        public string Title { get; set; }

        [DataMember(Order = 3)]
        public string Url { get; set; }

        [DataMember(Order = 4)]
        public string HdUrl { get; set; }

        [DataMember(Order = 5)]
        public string MediaType { get; set; }

        [DataMember(Order = 6)]
        public string Explanation { get; set; }

        [DataMember(Order = 7)]
        public string Copyright { get; set; }

        [DataMember(Order = 8)]
        public string LocalImagePath { get; set; }

        [DataMember(Order = 9)]
        public DateTime CachedAtUtc { get; set; }

        [DataMember(Order = 10)]
        public DateTime LastVerifiedUtc { get; set; }

        [DataMember(Order = 11)]
        public string Source { get; set; }

        [DataMember(Order = 12)]
        public bool IsFallbackImage { get; set; }

        [DataMember(Order = 13)]
        public bool LocalFileExistsAtLastCheck { get; set; }

        public static ApodCachedEntry FromEntry(ApodEntry entry)
        {
            return new ApodCachedEntry
            {
                Date = entry.Date,
                Title = entry.Title,
                Url = entry.Url,
                HdUrl = entry.HdUrl,
                MediaType = entry.MediaType,
                Explanation = entry.Explanation,
                Copyright = entry.Copyright,
                CachedAtUtc = DateTime.UtcNow,
                LastVerifiedUtc = DateTime.UtcNow,
                Source = entry.ResolvedFromSource,
                IsFallbackImage = entry.IsFallbackImage,
            };
        }

        public ApodEntry ToEntry()
        {
            return new ApodEntry
            {
                Date = Date,
                Title = Title,
                Url = Url,
                HdUrl = HdUrl,
                MediaType = MediaType,
                Explanation = Explanation,
                Copyright = Copyright,
                ResolvedFromSource = Source,
                IsFallbackImage = IsFallbackImage,
            };
        }
    }
}
