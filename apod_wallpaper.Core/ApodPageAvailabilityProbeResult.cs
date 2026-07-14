using System;
using System.Net;

namespace apod_wallpaper
{
    public sealed class ApodPageAvailabilityProbeResult
    {
        private ApodPageAvailabilityProbeResult(
            DateTime date,
            string expectedUrl,
            bool isAvailable,
            bool isUnavailable,
            HttpStatusCode? statusCode,
            string method,
            string redirectLocation,
            string errorMessage)
        {
            Date = date.Date;
            ExpectedUrl = expectedUrl;
            IsAvailable = isAvailable;
            IsUnavailable = isUnavailable;
            StatusCode = statusCode;
            Method = method;
            RedirectLocation = redirectLocation;
            ErrorMessage = errorMessage;
        }

        public DateTime Date { get; }
        public string ExpectedUrl { get; }
        public bool IsAvailable { get; }
        public bool IsUnavailable { get; }
        public HttpStatusCode? StatusCode { get; }
        public string Method { get; }
        public string RedirectLocation { get; }
        public string ErrorMessage { get; }

        public static ApodPageAvailabilityProbeResult Available(DateTime date, string expectedUrl, HttpStatusCode statusCode, string method)
        {
            return new ApodPageAvailabilityProbeResult(date, expectedUrl, true, false, statusCode, method, null, null);
        }

        public static ApodPageAvailabilityProbeResult Unavailable(DateTime date, string expectedUrl, HttpStatusCode statusCode, string method, string redirectLocation = null)
        {
            return new ApodPageAvailabilityProbeResult(date, expectedUrl, false, true, statusCode, method, redirectLocation, null);
        }

        public static ApodPageAvailabilityProbeResult Unknown(DateTime date, string expectedUrl, string method = null, HttpStatusCode? statusCode = null, string errorMessage = null)
        {
            return new ApodPageAvailabilityProbeResult(date, expectedUrl, false, false, statusCode, method, null, errorMessage);
        }
    }
}
