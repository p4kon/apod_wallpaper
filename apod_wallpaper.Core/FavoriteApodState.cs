using System.Collections.Generic;
using System.Runtime.Serialization;

namespace apod_wallpaper
{
    [DataContract]
    internal sealed class FavoriteApodState
    {
        [DataMember(Order = 1)]
        public int Version { get; set; }

        [DataMember(Order = 2)]
        public List<string> Dates { get; set; }

        public FavoriteApodState()
        {
            Version = 1;
            Dates = new List<string>();
        }
    }
}
