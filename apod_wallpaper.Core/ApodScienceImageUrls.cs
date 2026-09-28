using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace apod_wallpaper
{
    // NASA's dynamicimage route applies a default resize even without query parameters.
    // Map only the verified NASA asset namespace; unknown transformations stay intact.
    internal static class ApodScienceImageUrls
    {
        private const string Host = "assets.science.nasa.gov";
        private const string DynamicPrefix = "/dynamicimage/assets/science/";
        private const string OriginalPrefix = "/content/dam/science/";
        private const string PreviewQuery = "w=800&h=800&fit=clip";

        public static string GetPreviewUrl(string sourceUrl)
        {
            string asset;
            return TryGetAsset(sourceUrl, out asset)
                ? new UriBuilder(Uri.UriSchemeHttps, Host) { Path = DynamicPrefix + asset, Query = PreviewQuery }.Uri.AbsoluteUri
                : sourceUrl;
        }

        public static string GetOriginalUrl(string sourceUrl)
        {
            string asset;
            return TryGetAsset(sourceUrl, out asset)
                ? new UriBuilder(Uri.UriSchemeHttps, Host) { Path = OriginalPrefix + asset }.Uri.AbsoluteUri
                : sourceUrl;
        }

        private static bool TryGetAsset(string sourceUrl, out string asset)
        {
            asset = null;
            Uri uri;
            if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(uri.Host, Host, StringComparison.OrdinalIgnoreCase) || !uri.IsDefaultPort ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
                return false;

            var extension = Path.GetExtension(uri.AbsolutePath);
            if (!string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
                return false;

            if (uri.AbsolutePath.StartsWith(OriginalPrefix, StringComparison.Ordinal) && string.IsNullOrEmpty(uri.Query))
                asset = uri.AbsolutePath.Substring(OriginalPrefix.Length);
            else if (uri.AbsolutePath.StartsWith(DynamicPrefix, StringComparison.Ordinal) && HasKnownTransform(uri.Query))
                asset = uri.AbsolutePath.Substring(DynamicPrefix.Length);

            return !string.IsNullOrWhiteSpace(asset);
        }

        private static bool HasKnownTransform(string query)
        {
            if (string.IsNullOrEmpty(query))
                return true;

            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var part in query.TrimStart('?').Split('&'))
            {
                var pair = part.Split(new[] { '=' }, 2);
                if (pair.Length != 2 || !keys.Add(pair[0]))
                    return false;
                var value = Uri.UnescapeDataString(pair[1]);
                switch (pair[0])
                {
                    case "w":
                    case "h":
                        int pixels;
                        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out pixels) || pixels < 1 || pixels > 100000)
                            return false;
                        break;
                    case "fit":
                        if (value != "clip")
                            return false;
                        break;
                    case "crop":
                        if (value != "faces,focalpoint")
                            return false;
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }
    }
}
