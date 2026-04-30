using System;
using System.IO;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace apod_wallpaper.WinUI;

internal sealed class TrayIconController : IDisposable
{
    private const int GwlWndProc = -4;
    private const int WmClose = 0x0010;
    private const int WmApp = 0x8000;
    private const int WmLButtonDblClk = 0x0203;
    private const int WmRButtonUp = 0x0205;
    private const int WmContextMenu = 0x007B;
    private const int WmNull = 0x0000;

    private const uint NimAdd = 0x00000000;
    private const uint NimDelete = 0x00000002;

    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;

    private const uint LrLoadFromFile = 0x00000010;
    private const uint ImageIcon = 1;

    private const uint TpmLeftAlign = 0x0000;
    private const uint TpmBottomAlign = 0x0020;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCmd = 0x0100;

    private const int SwHide = 0;
    private const int SwRestore = 9;

    private const uint MfString = 0x00000000;

    private const uint MenuIdShow = 1001;
    private const uint MenuIdExit = 1002;

    private readonly MainWindow _owner;
    private readonly TraySpikeStatus _status;
    private readonly WndProc _subclassProc;
    private readonly uint _trayMessageId;
    private readonly IntPtr _windowHandle;

    private IntPtr _originalWndProc;
    private IntPtr _iconHandle;
    private bool _allowWindowClose;
    private bool _disposed;

    public TrayIconController(MainWindow owner, TraySpikeStatus status)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _windowHandle = WindowNative.GetWindowHandle(owner);
        _trayMessageId = WmApp + 1;
        _subclassProc = WindowProc;
    }

    public void Initialize()
    {
        _originalWndProc = SetWindowLongPtr(_windowHandle, GwlWndProc, _subclassProc);
        EnsureTrayIconVisible();
    }

    public void HideToTray()
    {
        if (_disposed)
            return;

        ShowWindow(_windowHandle, SwHide);
        _status.MarkWindowHidden();
    }

    public void RestoreFromTray(string reason)
    {
        if (_disposed)
            return;

        ShowWindow(_windowHandle, SwRestore);
        SetForegroundWindow(_windowHandle);
        _status.MarkWindowRestored(reason);
    }

    public void AllowClose()
    {
        _allowWindowClose = true;
        _status.MarkExitRequested();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        RemoveTrayIcon();

        if (_originalWndProc != IntPtr.Zero)
            SetWindowLongPtr(_windowHandle, GwlWndProc, _originalWndProc);

        if (_iconHandle != IntPtr.Zero)
        {
            DestroyIcon(_iconHandle);
            _iconHandle = IntPtr.Zero;
        }

        _disposed = true;
    }

    private void EnsureTrayIconVisible()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        _iconHandle = LoadImage(IntPtr.Zero, iconPath, ImageIcon, 0, 0, LrLoadFromFile);
        if (_iconHandle == IntPtr.Zero)
            throw new InvalidOperationException("Unable to load tray icon from " + iconPath + ".");

        var notifyIconData = CreateNotifyIconData();
        if (!Shell_NotifyIcon(NimAdd, ref notifyIconData))
            throw new InvalidOperationException("Unable to create tray icon in the packaged WinUI host.");

        _status.MarkTrayIconVisible();
    }

    private void RemoveTrayIcon()
    {
        var notifyIconData = CreateNotifyIconData();
        Shell_NotifyIcon(NimDelete, ref notifyIconData);
    }

    private NotifyIconData CreateNotifyIconData()
    {
        return new NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = _windowHandle,
            uID = 1,
            uFlags = NifMessage | NifIcon | NifTip,
            uCallbackMessage = _trayMessageId,
            hIcon = _iconHandle,
            szTip = "APOD Wallpaper",
        };
    }

    private IntPtr WindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WmClose && !_allowWindowClose)
        {
            HideToTray();
            return IntPtr.Zero;
        }

        if (msg == _trayMessageId)
        {
            var trayEvent = unchecked((uint)lParam.ToInt64());
            if (trayEvent == WmLButtonDblClk)
            {
                RestoreFromTray("Window restored from tray double-click.");
                return IntPtr.Zero;
            }

            if (trayEvent == WmRButtonUp || trayEvent == WmContextMenu)
            {
                ShowContextMenu();
                return IntPtr.Zero;
            }
        }

        return CallWindowProc(_originalWndProc, hwnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        _status.MarkContextMenuOpened();

        var menuHandle = CreatePopupMenu();
        if (menuHandle == IntPtr.Zero)
            return;

        try
        {
            AppendMenu(menuHandle, MfString, MenuIdShow, "Show");
            AppendMenu(menuHandle, MfString, MenuIdExit, "Exit");

            GetCursorPos(out var point);
            SetForegroundWindow(_windowHandle);
            var command = TrackPopupMenu(
                menuHandle,
                TpmLeftAlign | TpmBottomAlign | TpmRightButton | TpmReturnCmd,
                point.X,
                point.Y,
                0,
                _windowHandle,
                IntPtr.Zero);
            PostMessage(_windowHandle, WmNull, IntPtr.Zero, IntPtr.Zero);

            if (command == MenuIdShow)
            {
                RestoreFromTray("Window restored from tray context menu.");
                return;
            }

            if (command == MenuIdExit)
            {
                _ = _owner.ExitApplicationAsync();
            }
        }
        finally
        {
            DestroyMenu(menuHandle);
        }
    }

    private delegate IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;

        public uint dwState;
        public uint dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;

        public uint uVersionOrTimeout;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;

        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData lpData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WndProc newProc);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newProc);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
