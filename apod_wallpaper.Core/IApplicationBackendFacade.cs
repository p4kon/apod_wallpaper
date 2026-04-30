namespace apod_wallpaper
{
    public interface IApplicationBackendFacade :
        IApplicationSessionFacade,
        IApplicationSettingsFacade,
        IApplicationStorageFacade,
        IApodWorkflowFacade,
        IApodCalendarFacade
    {
    }
}
