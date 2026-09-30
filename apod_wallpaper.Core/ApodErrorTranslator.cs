using System;
using System.IO;
using System.Net.Http;

namespace apod_wallpaper
{
    internal static class ApodErrorTranslator
    {
        public static string ToUserMessage(Exception exception)
        {
            if (exception is ApodEntryUnavailableException)
                return "The selected APOD date is currently unavailable.";

            if (exception is ApodScienceRequestException scienceError && scienceError.Status == 429)
                return "NASA is receiving too many requests. Please try again later.";

            if (exception is HttpRequestException || exception is ApodScienceRequestException)
                return "Unable to reach NASA APOD right now. Check your internet connection and try again.";

            if (exception is TimeoutException)
                return "The APOD request timed out. Please try again.";

            if (exception is InvalidDataException)
                return "The APOD response could not be read. Please try again later.";

            if (exception is InvalidOperationException)
                return exception.Message;

            return "Something went wrong while processing the APOD request.";
        }
    }
}
