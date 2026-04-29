namespace apod_wallpaper
{
    public sealed class OperationError
    {
        public OperationError(OperationErrorCode code, string message, bool retryable = false, string technicalDetails = null)
        {
            Code = code;
            Message = string.IsNullOrWhiteSpace(message) ? "Operation failed." : message;
            Retryable = retryable;
            TechnicalDetails = technicalDetails;
        }

        public OperationErrorCode Code { get; }
        public string Message { get; }
        public bool Retryable { get; }
        public string TechnicalDetails { get; }
    }
}
