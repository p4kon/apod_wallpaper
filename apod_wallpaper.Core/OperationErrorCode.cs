namespace apod_wallpaper
{
    public enum OperationErrorCode
    {
        Unknown,
        InvalidArgument,
        InitializationFailed,
        SettingsReadFailed,
        SettingsWriteFailed,
        StateUpdateFailed,
        ValidationFailed,
        LoggingFailed,
        StorageFailed,
        WorkflowFailed,
        ShutdownFailed,
    }
}
