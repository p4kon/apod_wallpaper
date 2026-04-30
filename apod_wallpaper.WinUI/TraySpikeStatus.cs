using System;

namespace apod_wallpaper.WinUI;

internal sealed class TraySpikeStatus
{
    public event EventHandler? Changed;

    public bool IsTrayIconVisible { get; private set; }

    public bool IsWindowHiddenToTray { get; private set; }

    public int HideCount { get; private set; }

    public int RestoreCount { get; private set; }

    public string LastAction { get; private set; } = "Tray spike is starting.";

    public DateTime? LastBackendCheckUtc { get; private set; }

    public void MarkTrayIconVisible()
    {
        IsTrayIconVisible = true;
        LastAction = "Tray icon created.";
        RaiseChanged();
    }

    public void MarkWindowHidden()
    {
        IsWindowHiddenToTray = true;
        HideCount++;
        LastAction = "Window hidden to tray.";
        RaiseChanged();
    }

    public void MarkWindowRestored(string reason)
    {
        IsWindowHiddenToTray = false;
        RestoreCount++;
        LastAction = reason;
        RaiseChanged();
    }

    public void MarkContextMenuOpened()
    {
        LastAction = "Tray context menu opened.";
        RaiseChanged();
    }

    public void MarkBackendCheck()
    {
        LastBackendCheckUtc = DateTime.UtcNow;
        LastAction = "Backend snapshot refreshed.";
        RaiseChanged();
    }

    public void MarkExitRequested()
    {
        LastAction = "Exit requested from tray or UI.";
        RaiseChanged();
    }

    private void RaiseChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
