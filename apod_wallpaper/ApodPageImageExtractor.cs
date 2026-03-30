using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace apod_wallpaper
{
    internal static class ApodPageImageExtractor
    {
        private static readonly Regex AnchorImageRegex = new Regex(
            "<a\\b[^>]*href\\s*=\\s*[\"'](?<href>[^\"']+)[\"'][^>]*>\\s*<img\\b[^>]*src\\s*=\\s*[\"'](?<src>[^\"']+)[\"'][^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex ImageRegex = new Regex(
            "<img\\b[^>]*src\\s*=\\s*[\"'](?<src>[^\"']+)[\"'][^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        public static bool TryExtract(string html, string pageUrl, out string previewUrl, out string imageUrl)
        {
            previewUrl = null;
            imageUrl = null;

            if (string.IsNullOrWhiteSpace(html) || string.IsNullOrWhiteSpace(pageUrl))
                return false;

            var baseUri = new Uri(pageUrl, UriKind.Absolute);
            var candidates = new List<Candidate>();

            foreach (Match match in AnchorImageRegex.Matches(html))
            {
                var href = ToAbsoluteUrl(baseUri, match.Groups["href"].Value);
                var src = ToAbsoluteUrl(baseUri, match.Groups["src"].Value);
                candidates.Add(new Candidate(src, href));
            }

            foreach (Match match in ImageRegex.Matches(html))
            {
                var src = ToAbsoluteUrl(baseUri, match.Groups["src"].Value);
                candidates.Add(new Candidate(src, src));
            }

            var bestCandidate = candidates
                .Where(candidate => candidate.HasAnyImage)
                .OrderByDescending(candidate => candidate.Score)
                .FirstOrDefault();

            if (bestCandidate == null)
                return false;

            previewUrl = bestCandidate.PreviewUrl;
            imageUrl = bestCandidate.ImageUrl;
            return !string.IsNullOrWhiteSpace(previewUrl) || !string.IsNullOrWhiteSpace(imageUrl);
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

            switch (extension.ToLowerInvariant())
            {
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".gif":
                case ".bmp":
                case ".webp":
                case ".tif":
                case ".tiff":
                    return true;
                default:
                    return false;
            }
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
        }
    }
}
