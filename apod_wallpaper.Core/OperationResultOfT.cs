namespace apod_wallpaper
{
    public sealed class OperationResult<T> : OperationResult
    {
        private OperationResult(bool succeeded, T value, OperationError error)
            : base(succeeded, error)
        {
            Value = value;
        }

        public T Value { get; }

        public static OperationResult<T> Success(T value)
        {
            return new OperationResult<T>(true, value, null);
        }

        public new static OperationResult<T> Failure(OperationError error)
        {
            return new OperationResult<T>(false, default(T), error ?? new OperationError(OperationErrorCode.Unknown, "Operation failed."));
        }
    }
}
