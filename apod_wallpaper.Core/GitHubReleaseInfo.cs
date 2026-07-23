using System.Runtime.Serialization;

namespace apod_wallpaper
{
    [DataContract]
    internal sealed class GitHubReleaseInfo
    {
        [DataMember(Name = "tag_name")]
        public string TagName { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "html_url")]
        public string HtmlUrl { get; set; }

        [DataMember(Name = "prerelease")]
        public bool Prerelease { get; set; }
    }
}
