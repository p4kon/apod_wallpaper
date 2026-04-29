namespace apod_wallpaper
{
    public interface IApplicationBackendFacade :
        IApplicationSessionFacade,
        IApplicationSettingsFacade,
        IApodWorkflowFacade,
        IApodCalendarFacade,
        IApplicationDiagnosticsFacade
    {
    }
}
