namespace apod_wallpaper
{
    public class OperationResult
    {
        protected OperationResult(bool succeeded, OperationError error)
        {
            Succeeded = succeeded;
            Error = error;
        }

        public bool Succeeded { get; }
        public OperationError Error { get; }

        public static OperationResult Success()
        {
            return new OperationResult(true, null);
        }

        public static OperationResult Failure(OperationError error)
        {
            return new OperationResult(false, error ?? new OperationError(OperationErrorCode.Unknown, "Operation failed."));
        }
    }
}
