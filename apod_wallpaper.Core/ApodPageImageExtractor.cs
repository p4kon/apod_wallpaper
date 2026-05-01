using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace apod_wallpaper
{
    internal static class ApodPageImageExtractor
    {
        private static readonly Regex AnchorHrefRegex = new Regex(
            "<a\\b[^>]*href\\s*=\\s*[\"'](?<href>[^\"']+)[\"'][^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex AnchorImageRegex = new Regex(
            "<a\\b[^>]*href\\s*=\\s*[\"'](?<href>[^\"']+)[\"'][^>]*>\\s*<img\\b[^>]*src\\s*=\\s*[\"'](?<src>[^\"']+)[\"'][^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex ImageRegex = new Regex(
            "<img\\b[^>]*src\\s*=\\s*[\"'](?<src>[^\"']+)[\"'][^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex ImagePathRegex = new Regex(
            "(?<url>(?:https?:)?//[^\\s\"'<>]+\\.(?:jpg|jpeg|png|gif|bmp|webp|tif|tiff)|(?:\\.?\\.?/)?image/[^\\s\"'<>]+\\.(?:jpg|jpeg|png|gif|bmp|webp|tif|tiff))",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex VideoTagSourceRegex = new Regex(
            "<source\\b[^>]*src\\s*=\\s*[\"'](?<src>[^\"']+)[\"'][^>]*type\\s*=\\s*[\"']video/[^\"']+[\"'][^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex VideoTagRegex = new Regex(
            "<video\\b[^>]*>.*?</video>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex IframeRegex = new Regex(
            "<iframe\\b[^>]*src\\s*=\\s*[\"'](?<src>[^\"']+)[\"'][^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex VideoPathRegex = new Regex(
            "(?<url>(?:https?:)?//[^\\s\"'<>]+\\.(?:mp4|mov|webm|m4v)|(?:\\.?\\.?/)?image/[^\\s\"'<>]+\\.(?:mp4|mov|webm|m4v)|https?:\\/\\/(?:www\\.)?youtube\\.com\\/embed\\/[^\\s\"'<>]+|https?:\\/\\/(?:www\\.)?youtube\\.com\\/watch\\?[^\\s\"'<>]+|https?:\\/\\/youtu\\.be\\/[^\\s\"'<>]+)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex ApodDateHeadingRegex = new Regex(
            "\\b\\d{4}\\s+(?:January|February|March|April|May|June|July|August|September|October|November|December)\\s+\\d{1,2}\\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool TryExtract(string html, string pageUrl, out string previewUrl, out string imageUrl)
        {
            previewUrl = null;
            imageUrl = null;

            if (string.IsNullOrWhiteSpace(html) || string.IsNullOrWhiteSpace(pageUrl))
                return false;

            var baseUri = new Uri(pageUrl, UriKind.Absolute);
            var relevantHtml = ExtractRelevantHtml(html);
            if (string.IsNullOrWhiteSpace(relevantHtml))
                return false;

            if (relevantHtml.IndexOf("<video", StringComparison.OrdinalIgnoreCase) >= 0 ||
                relevantHtml.IndexOf("type=\"video/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                relevantHtml.IndexOf("type='video/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            var candidates = new List<Candidate>();

            foreach (Match match in AnchorImageRegex.Matches(relevantHtml))
            {
                var href = ToAbsoluteUrl(baseUri, match.Groups["href"].Value);
                var src = ToAbsoluteUrl(baseUri, match.Groups["src"].Value);
                candidates.Add(new Candidate(src, href));
            }

            foreach (Match match in AnchorHrefRegex.Matches(relevantHtml))
            {
                var href = ToAbsoluteUrl(baseUri, match.Groups["href"].Value);
                candidates.Add(new Candidate(null, href));
            }

            foreach (Match match in ImageRegex.Matches(relevantHtml))
            {
                var src = ToAbsoluteUrl(baseUri, match.Groups["src"].Value);
                candidates.Add(new Candidate(src, src));
            }

            foreach (Match match in ImagePathRegex.Matches(relevantHtml))
            {
                var imagePath = ToAbsoluteUrl(baseUri, match.Groups["url"].Value);
                candidates.Add(new Candidate(imagePath, imagePath));
            }

            var bestCandidate = candidates
                .Where(candidate => candidate.HasAnyImage)
                .OrderByDescending(candidate => candidate.Score)
                .FirstOrDefault();

            if (bestCandidate == null)
                return false;

            previewUrl = bestCandidate.PreviewUrl ?? bestCandidate.ImageUrl;
            imageUrl = bestCandidate.ImageUrl ?? bestCandidate.PreviewUrl;
            return !string.IsNullOrWhiteSpace(previewUrl) || !string.IsNullOrWhiteSpace(imageUrl);
        }

        public static bool TryExtractVideo(string html, string pageUrl, out string videoUrl)
        {
            videoUrl = null;

            if (string.IsNullOrWhiteSpace(html) || string.IsNullOrWhiteSpace(pageUrl))
                return false;

            var baseUri = new Uri(pageUrl, UriKind.Absolute);
            var relevantHtml = ExtractRelevantHtml(html);
            if (string.IsNullOrWhiteSpace(relevantHtml))
                return false;

            foreach (Match match in VideoTagSourceRegex.Matches(relevantHtml))
            {
                var sourceUrl = ToAbsoluteUrl(baseUri, match.Groups["src"].Value);
                if (LooksLikeVideoUrl(sourceUrl))
                {
                    videoUrl = sourceUrl;
                    return true;
                }
            }

            foreach (Match match in IframeRegex.Matches(relevantHtml))
            {
                var sourceUrl = ToAbsoluteUrl(baseUri, match.Groups["src"].Value);
                if (LooksLikeVideoUrl(sourceUrl))
                {
                    videoUrl = sourceUrl;
                    return true;
                }
            }

            foreach (Match match in VideoPathRegex.Matches(relevantHtml))
            {
                var sourceUrl = ToAbsoluteUrl(baseUri, match.Groups["url"].Value);
                if (LooksLikeVideoUrl(sourceUrl))
                {
                    videoUrl = sourceUrl;
                    return true;
                }
            }

            if (VideoTagRegex.IsMatch(relevantHtml))
            {
                videoUrl = pageUrl;
                return true;
            }

            return false;
        }

        private static string ExtractRelevantHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return html;

            var dateHeadingIndex = FindDateHeadingIndex(html);
            var startIndex = dateHeadingIndex >= 0
                ? FindRelevantBlockStart(html, dateHeadingIndex)
                : 0;

            var endIndexSearchStart = dateHeadingIndex >= 0 ? dateHeadingIndex : startIndex;
            var endIndex = html.IndexOf("</center>", endIndexSearchStart, StringComparison.OrdinalIgnoreCase);
            if (endIndex < 0)
                endIndex = Math.Min(html.Length, startIndex + 4000);
            else
                endIndex += "</center>".Length;

            if (endIndex <= startIndex)
                return html;

            return html.Substring(startIndex, endIndex - startIndex);
        }

        private static int FindDateHeadingIndex(string html)
        {
            var match = ApodDateHeadingRegex.Match(html);
            return match.Success ? match.Index : -1;
        }

        private static int FindRelevantBlockStart(string html, int dateHeadingIndex)
        {
            if (dateHeadingIndex < 0)
                return 0;

            var centerIndex = html.LastIndexOf("<center", dateHeadingIndex, StringComparison.OrdinalIgnoreCase);
            if (centerIndex >= 0)
                return centerIndex;

            return dateHeadingIndex;
        }

        public static bool LooksLikeImageUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) && !Uri.TryCreate(url, UriKind.Relative, out uri))
                return false;

            var path = uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString;
            var extension = System.IO.Path.GetExtension(path);
            if (string.IsNullOrWhiteSpace(extension))
                return false;

            return ImageFormatCatalog.IsSupportedImageExtension(extension);
        }

        public static bool LooksLikeVideoUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (url.IndexOf("youtube.com/embed/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                url.IndexOf("youtube.com/watch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                url.IndexOf("youtu.be/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) && !Uri.TryCreate(url, UriKind.Relative, out uri))
                return false;

            var path = uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString;
            var extension = System.IO.Path.GetExtension(path);
            if (string.IsNullOrWhiteSpace(extension))
                return false;

            return extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".mov", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".webm", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".m4v", StringComparison.OrdinalIgnoreCase);
        }

        private static string ToAbsoluteUrl(Uri baseUri, string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                return null;

            Uri absoluteUri;
            if (Uri.TryCreate(baseUri, candidate.Trim(), out absoluteUri))
                return absoluteUri.ToString();

            return null;
        }

        private sealed class Candidate
        {
            public Candidate(string previewUrl, string imageUrl)
            {
                PreviewUrl = LooksLikeImageUrl(previewUrl) ? previewUrl : null;
                ImageUrl = LooksLikeImageUrl(imageUrl) ? imageUrl : null;
            }

            public string PreviewUrl { get; }
            public string ImageUrl { get; }

            public bool HasAnyImage => !string.IsNullOrWhiteSpace(PreviewUrl) || !string.IsNullOrWhiteSpace(ImageUrl);

            public int Score
            {
                get
                {
                    var score = 0;

                    if (!string.IsNullOrWhiteSpace(ImageUrl))
                        score += 100;
                    if (!string.IsNullOrWhiteSpace(PreviewUrl))
                        score += 40;

                    if (ContainsApodImagePath(ImageUrl))
                        score += 30;
                    if (ContainsApodImagePath(PreviewUrl))
                        score += 20;
                    if (LooksLikeOriginalFile(ImageUrl))
                        score += 25;
                    if (LooksLikeOriginalFile(PreviewUrl))
                        score += 10;

                    if (!string.IsNullOrWhiteSpace(ImageUrl) &&
                        !string.IsNullOrWhiteSpace(PreviewUrl) &&
                        !string.Equals(ImageUrl, PreviewUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 10;
                    }

                    return score;
                }
            }

            private static bool ContainsApodImagePath(string url)
            {
                return !string.IsNullOrWhiteSpace(url) &&
                    url.IndexOf("/image/", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            private static bool LooksLikeOriginalFile(string url)
            {
                return !string.IsNullOrWhiteSpace(url) &&
                    (url.IndexOf("original", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     url.IndexOf("_full", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     url.IndexOf("_large", StringComparison.OrdinalIgnoreCase) >= 0);
            }
        }
    }
}
