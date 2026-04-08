using System;
using System.Runtime.Serialization;

namespace apod_wallpaper
{
    [DataContract]
    public sealed class ApodEntry
    {
        [DataMember(Name = "date")]
        public string Date { get; set; }

        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "url")]
        public string Url { get; set; }

        [DataMember(Name = "hdurl")]
        public string HdUrl { get; set; }

        [DataMember(Name = "media_type")]
        public string MediaType { get; set; }

        [DataMember(Name = "explanation")]
        public string Explanation { get; set; }

        [DataMember(Name = "copyright")]
        public string Copyright { get; set; }

        [IgnoreDataMember]
        public string ResolvedFromSource { get; set; }

        [IgnoreDataMember]
        public bool IsFallbackImage { get; set; }

        public bool HasImage
        {
            get
            {
                return string.Equals(MediaType, "image", StringComparison.OrdinalIgnoreCase) ||
                    ApodPageImageExtractor.LooksLikeImageUrl(HdUrl) ||
                    ApodPageImageExtractor.LooksLikeImageUrl(Url);
            }
        }

        public string PreviewImageUrl
        {
            get
            {
                return !string.IsNullOrWhiteSpace(Url) ? Url : HdUrl;
            }
        }

        public string BestImageUrl
        {
            get
            {
                return !string.IsNullOrWhiteSpace(HdUrl) ? HdUrl : Url;
            }
        }
    }
}
