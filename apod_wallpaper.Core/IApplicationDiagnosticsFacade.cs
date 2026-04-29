using System;

namespace apod_wallpaper
{
    public interface IApplicationDiagnosticsFacade
    {
        OperationResult<string> GetUserFriendlyErrorMessage(Exception exception, string fallbackMessage = null);
        OperationResult LogWarning(string message, Exception exception = null);
    }
}
