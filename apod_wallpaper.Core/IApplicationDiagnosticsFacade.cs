using System;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    public interface IApplicationDiagnosticsFacade
    {
        Task<OperationResult<string>> GetUserFriendlyErrorMessageAsync(Exception exception, string fallbackMessage = null);
        Task<OperationResult> LogWarningAsync(string message, Exception exception = null);
    }
}
