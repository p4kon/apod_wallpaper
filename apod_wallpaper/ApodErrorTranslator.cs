using System;
using System.Net.Http;

namespace apod_wallpaper
{
    internal static class ApodErrorTranslator
    {
        public static string ToUserMessage(Exception exception)
        {
            if (exception is HttpRequestException)
                return "Unable to reach NASA APOD right now. Check your internet connection and try again.";

            if (exception is InvalidOperationException)
                return exception.Message;

            return "Something went wrong while processing the APOD request.";
        }
    }
}
