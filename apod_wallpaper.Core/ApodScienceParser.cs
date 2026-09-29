using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace apod_wallpaper
{
    internal sealed class ApodScienceRecord
    {
        public ApodScienceRecord(ApodEntry entry, string postUrl, string sourcePreviewUrl = null, string sourceOriginalUrl = null)
        {
            Entry = entry;
            PostUrl = postUrl;
            SourcePreviewUrl = sourcePreviewUrl;
            SourceOriginalUrl = sourceOriginalUrl;
        }

        public ApodEntry Entry { get; }
        public string PostUrl { get; }
        public string SourcePreviewUrl { get; }
        public string SourceOriginalUrl { get; }
    }

    internal static class ApodScienceParser
    {
        private const string Endpoint = "https://science.nasa.gov/wp-json/wp/v2/apod-basic/";
        internal const int MaximumJsonCharacters = 1024 * 1024;
        private static readonly Regex Body = Pattern(@"<body\b[^>]*>(?<body>.*?)</body\s*>");
        private static readonly Regex Center = Pattern(@"<center\b[^>]*>(?<content>.*?)</center\s*>");
        private static readonly Regex DateHeading = Pattern(@"\b\d{4}\s+(?:January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2}\b");
        private static readonly Regex LinkedImage = Pattern(@"<a\b(?<anchor>[^>]*)>\s*<img\b(?<image>[^>]*)>");
        private static readonly Regex Image = Pattern(@"<img\b(?<image>[^>]*)>");
        private static readonly Regex EmbeddedMedia = Pattern(@"<(?:iframe|video|embed|object)\b");
        private static readonly Regex Hidden = Pattern(@"<script\b[^>]*>.*?</script\s*>|<style\b[^>]*>.*?</style\s*>|<!--.*?-->");
        private static readonly Regex Breaks = Pattern(@"<br\b[^>]*>|</(?:p|div|li)\s*>");
        private static readonly Regex Tags = Pattern(@"<[^>]*>");
        private static readonly Regex Spaces = Pattern(@"[^\S\n]+");
        private static readonly Regex EmptyLines = Pattern(@"\n\s*\n+");

        public static string BuildUrl(DateTime date)
        {
            return Endpoint + date.ToString("yyMMdd", CultureInfo.InvariantCulture);
        }

        internal static IReadOnlyList<ApodEntry> ParseRangePage(string json, DateTime start, DateTime end)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumJsonCharacters || !json.TrimStart().StartsWith("[", StringComparison.Ordinal))
                throw new InvalidDataException("Invalid NASA Science range document.");
            try
            {
                List<ScienceDto> documents;
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    documents = (List<ScienceDto>)new DataContractJsonSerializer(typeof(List<ScienceDto>)).ReadObject(stream);
                if (documents == null || documents.Count > 31) throw new InvalidDataException("Invalid NASA Science page size.");
                var entries = new List<ApodEntry>();
                foreach (var document in documents)
                {
                    DateTime date;
                    if (document == null || !DateTime.TryParseExact(document.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                        || date < start.Date || date > end.Date)
                        throw new InvalidDataException("NASA Science range contains an unexpected date.");
                    using (var stream = new MemoryStream())
                    {
                        new DataContractJsonSerializer(typeof(ScienceDto)).WriteObject(stream, document);
                        entries.Add(Parse(Encoding.UTF8.GetString(stream.ToArray()), date).Entry);
                    }
                }
                return entries;
            }
            catch (SerializationException ex) { throw new InvalidDataException("Invalid NASA Science range JSON.", ex); }
        }

        public static ApodScienceRecord Parse(string json, DateTime requestedDate)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumJsonCharacters || !json.TrimStart().StartsWith("{", StringComparison.Ordinal))
                throw new InvalidDataException("Invalid NASA Science JSON document.");

            try
            {
                ScienceDto document;
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    document = (ScienceDto)new DataContractJsonSerializer(typeof(ScienceDto)).ReadObject(stream);

                if (document == null || document.Date != requestedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                    throw new InvalidDataException("NASA Science returned a different or missing date.");

                var postUrl = ValidatePostUrl(document.Permalink);
                var title = PlainText(document.Title);
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(document.BasicHtml))
                    throw new InvalidDataException("NASA Science is missing title or basic HTML.");

                var body = Body.Match(Hidden.Replace(document.BasicHtml, string.Empty));
                if (!body.Success)
                    throw new InvalidDataException("NASA Science basic HTML has no complete body.");

                // Only the date-bearing center in the body owns APOD media. Head metadata
                // and explanation links can point to unrelated images or generic posters.
                string mediaBlock = null;
                foreach (Match center in Center.Matches(body.Groups["body"].Value))
                {
                    var content = center.Groups["content"].Value;
                    var heading = DateHeading.Match(PlainText(content));
                    if (!heading.Success)
                        continue;
                    DateTime htmlDate;
                    if (!DateTime.TryParseExact(Spaces.Replace(heading.Value, " "), "yyyy MMMM d", CultureInfo.InvariantCulture, DateTimeStyles.None, out htmlDate) || htmlDate.Date != requestedDate.Date)
                        throw new InvalidDataException("NASA Science HTML date does not match its JSON date.");
                    mediaBlock = content;
                    break;
                }
                if (mediaBlock == null)
                    throw new InvalidDataException("NASA Science primary media block is missing.");

                var kind = (document.MediaType ?? string.Empty).Trim().ToLowerInvariant();
                if (kind != "" && kind != "image" && kind != "video" && kind != "iframe" && kind != "other")
                    throw new InvalidDataException("Unknown NASA Science media type.");

                var explanation = PlainText(document.Explanation);
                explanation = Regex.Replace(explanation, @"^(?:Explanation\s*:\s*)+", string.Empty, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
                var entry = new ApodEntry
                {
                    Date = document.Date,
                    PostUrl = postUrl,
                    Title = title,
                    Explanation = explanation,
                    Copyright = PlainText(document.Copyright ?? document.Credit),
                    ResolvedFromSource = "nasa_science",
                    MediaType = "other",
                };

                if (kind == "video" || kind == "iframe" || EmbeddedMedia.IsMatch(mediaBlock))
                {
                    entry.MediaType = "video";
                    return new ApodScienceRecord(entry, postUrl);
                }

                var linked = LinkedImage.Match(mediaBlock);
                var image = linked.Success ? linked : Image.Match(mediaBlock);
                if (!image.Success)
                {
                    if (kind == "image")
                        throw new InvalidDataException("NASA Science image publication has no primary image.");
                    return new ApodScienceRecord(entry, postUrl);
                }
                if (kind == "other")
                    return new ApodScienceRecord(entry, postUrl);

                var preview = ValidateImageUrl(Attribute(image.Groups["image"].Value, "src"), postUrl);
                var original = preview;
                if (linked.Success)
                {
                    var href = Attribute(linked.Groups["anchor"].Value, "href");
                    if (ApodPageImageExtractor.LooksLikeImageUrl(WebUtility.HtmlDecode(href)))
                        original = ValidateImageUrl(href, postUrl);
                }
                entry.MediaType = "image";
                entry.Url = ApodScienceImageUrls.GetPreviewUrl(preview);
                entry.HdUrl = ApodScienceImageUrls.GetOriginalUrl(original);
                entry.SourcePreviewUrl = preview;
                entry.SourceOriginalUrl = original;
                return new ApodScienceRecord(entry, postUrl, preview, original);
            }
            catch (SerializationException ex)
            {
                throw new InvalidDataException("NASA Science JSON could not be parsed.", ex);
            }
            catch (RegexMatchTimeoutException ex)
            {
                throw new InvalidDataException("NASA Science HTML exceeded the parsing budget.", ex);
            }
        }

        private static Regex Pattern(string pattern)
        {
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        }

        private static string Attribute(string attributes, string name)
        {
            var match = Regex.Match(attributes, @"(?:^|\s)" + name + @"\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s>]+))", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            return match.Success ? match.Groups["value"].Value : null;
        }

        private static string ValidatePostUrl(string value)
        {
            var normalized = NormalizePostUrl(value);
            if (normalized == null)
                throw new InvalidDataException("NASA Science canonical URL is invalid.");
            return normalized;
        }

        internal static string NormalizePostUrl(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(uri.Host, "science.nasa.gov", StringComparison.OrdinalIgnoreCase) ||
                !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo) ||
                !uri.AbsolutePath.StartsWith("/image-article/", StringComparison.Ordinal))
                return null;
            return uri.AbsoluteUri;
        }

        private static string ValidateImageUrl(string value, string postUrl)
        {
            Uri uri;
            if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(new Uri(postUrl), WebUtility.HtmlDecode(value), out uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
                uri.HostNameType != UriHostNameType.Dns || !uri.Host.Contains(".") ||
                uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
                !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo) ||
                !ApodPageImageExtractor.LooksLikeImageUrl(uri.AbsoluteUri) ||
                uri.AbsolutePath.IndexOf("news-thumbnail", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidDataException("NASA Science primary image URL is invalid or a placeholder.");
            return uri.AbsoluteUri;
        }

        private static string PlainText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var text = Breaks.Replace(Hidden.Replace(value, string.Empty), "\n");
            text = WebUtility.HtmlDecode(Tags.Replace(text, " ")).Replace('\u00a0', ' ').Replace("\r", string.Empty);
            text = Spaces.Replace(text, " ");
            text = Regex.Replace(text, @" *\n *", "\n", RegexOptions.None, TimeSpan.FromSeconds(1));
            return EmptyLines.Replace(text, "\n").Trim();
        }

        [DataContract]
        private sealed class ScienceDto
        {
            [DataMember(Name = "date")] public string Date { get; set; }
            [DataMember(Name = "title")] public string Title { get; set; }
            [DataMember(Name = "permalink")] public string Permalink { get; set; }
            [DataMember(Name = "media_type")] public string MediaType { get; set; }
            [DataMember(Name = "explanation")] public string Explanation { get; set; }
            [DataMember(Name = "copyright")] public string Copyright { get; set; }
            [DataMember(Name = "credit")] public string Credit { get; set; }
            [DataMember(Name = "basic_html")] public string BasicHtml { get; set; }
        }
    }
}
