using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace apod_wallpaper
{
    public enum WallpaperStyle
    {
        Fill,
        Fit,
        Stretch,
        Tile,
        Center,
        Span,
        Smart,
    }

    internal static class WallpaperNative
    {
        [ComImport]
        [Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDesktopWallpaper
        {
            void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorID, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
            [return: MarshalAs(UnmanagedType.LPWStr)]
            string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorID);
            void GetMonitorDevicePathAt(uint monitorIndex, [MarshalAs(UnmanagedType.LPWStr)] out string monitorID);
            uint GetMonitorDevicePathCount();
            void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorID, out RECT displayRect);
            void SetBackgroundColor(uint color);
            uint GetBackgroundColor();
            void SetPosition(DesktopWallpaperPosition position);
            DesktopWallpaperPosition GetPosition();
            void SetSlideshow(IntPtr items);
            IntPtr GetSlideshow();
            void SetSlideshowOptions(uint options, uint slideshowTick);
            void GetSlideshowOptions(out uint options, out uint slideshowTick);
            void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string monitorID, uint direction);
            uint GetStatus();
            bool Enable();
        }

        [ComImport]
        [Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD")]
        private class DesktopWallpaperComObject
        {
        }

        private enum DesktopWallpaperPosition
        {
            Center = 0,
            Tile = 1,
            Stretch = 2,
            Fit = 3,
            Fill = 4,
            Span = 5,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const string DesktopRegistryPath = @"Control Panel\Desktop";
        private const string HistoryRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers";
        private const string WallpaperStyleRegistryPath = "WallpaperStyle";
        private const string TileWallpaperRegistryPath = "TileWallpaper";
        private const int HistoryMaxEntries = 5;

        private const int SpiSetDesktopWallpaper = 20;
        private const int SpifUpdateIniFile = 0x01;
        private const int SpifSendWinIniChange = 0x02;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        private static State? _backupState;
        private static bool _historyRestored;

        private struct Config
        {
            public int Style;
            public bool IsTile;
        }

        private struct State
        {
            public Config Config;
            public string[] History;
            public string Wallpaper;
        }

        private static int GetRegistryValue(RegistryKey key, string name, int defaultValue)
        {
            return int.Parse((string)key.GetValue(name) ?? defaultValue.ToString());
        }

        private static bool GetRegistryValue(RegistryKey key, string name, bool defaultValue)
        {
            return ((string)key.GetValue(name) ?? (defaultValue ? "1" : "0")) == "1";
        }

        private static void SetRegistryValue(RegistryKey key, string name, int value)
        {
            key.SetValue(name, value.ToString());
        }

        private static void SetRegistryValue(RegistryKey key, string name, bool value)
        {
            key.SetValue(name, value ? "1" : "0");
        }

        private static Config GetWallpaperConfig()
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(DesktopRegistryPath, true);

            return new Config
            {
                Style = GetRegistryValue(key, WallpaperStyleRegistryPath, 0),
                IsTile = GetRegistryValue(key, TileWallpaperRegistryPath, false),
            };
        }

        private static void SetWallpaperConfig(Config value)
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(DesktopRegistryPath, true);
            SetRegistryValue(key, WallpaperStyleRegistryPath, value.Style);
            SetRegistryValue(key, TileWallpaperRegistryPath, value.IsTile);
        }

        private static DesktopWallpaperPosition ToDesktopWallpaperPosition(WallpaperStyle style)
        {
            switch (style)
            {
                case WallpaperStyle.Fill:
                    return DesktopWallpaperPosition.Fill;
                case WallpaperStyle.Fit:
                    return DesktopWallpaperPosition.Fit;
                case WallpaperStyle.Stretch:
                    return DesktopWallpaperPosition.Stretch;
                case WallpaperStyle.Tile:
                    return DesktopWallpaperPosition.Tile;
                case WallpaperStyle.Center:
                    return DesktopWallpaperPosition.Center;
                case WallpaperStyle.Span:
                    return DesktopWallpaperPosition.Span;
                case WallpaperStyle.Smart:
                    return DesktopWallpaperPosition.Fit;
                default:
                    throw new ArgumentOutOfRangeException(nameof(style));
            }
        }

        private static void SetStyle(WallpaperStyle style)
        {
            switch (style)
            {
                case WallpaperStyle.Fill:
                    SetWallpaperConfig(new Config { Style = 10, IsTile = false });
                    break;
                case WallpaperStyle.Fit:
                    SetWallpaperConfig(new Config { Style = 6, IsTile = false });
                    break;
                case WallpaperStyle.Stretch:
                    SetWallpaperConfig(new Config { Style = 2, IsTile = false });
                    break;
                case WallpaperStyle.Tile:
                    SetWallpaperConfig(new Config { Style = 0, IsTile = true });
                    break;
                case WallpaperStyle.Center:
                    SetWallpaperConfig(new Config { Style = 0, IsTile = false });
                    break;
                case WallpaperStyle.Span:
                    SetWallpaperConfig(new Config { Style = 22, IsTile = false });
                    break;
                case WallpaperStyle.Smart:
                    SetWallpaperConfig(new Config { Style = 6, IsTile = false });
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(style));
            }
        }

        private static bool TrySetWithDesktopWallpaperApi(string filename, WallpaperStyle style)
        {
            IDesktopWallpaper desktopWallpaper = null;
            try
            {
                desktopWallpaper = (IDesktopWallpaper)new DesktopWallpaperComObject();
                desktopWallpaper.SetPosition(ToDesktopWallpaperPosition(style));
                desktopWallpaper.SetWallpaper(null, filename);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (desktopWallpaper != null && Marshal.IsComObject(desktopWallpaper))
                    Marshal.ReleaseComObject(desktopWallpaper);
            }
        }

        private static void ChangeWallpaper(string filename)
        {
            SystemParametersInfo(SpiSetDesktopWallpaper, 0, filename, SpifUpdateIniFile | SpifSendWinIniChange);
        }

        private static void RestoreHistory()
        {
            if (_historyRestored)
                return;

            if (!_backupState.HasValue)
                throw new InvalidOperationException("You must call BackupState() before.");

            var backupState = _backupState.Value;

            using (var key = Registry.CurrentUser.OpenSubKey(HistoryRegistryPath, true))
            {
                for (var i = 0; i < HistoryMaxEntries; i++)
                {
                    if (backupState.History[i] != null)
                        key.SetValue($"BackgroundHistoryPath{i}", backupState.History[i], RegistryValueKind.String);
                }
            }

            _historyRestored = true;
        }

        private static void BackupState()
        {
            var history = new string[HistoryMaxEntries];

            using (var key = Registry.CurrentUser.OpenSubKey(HistoryRegistryPath, true))
            {
                for (var i = 0; i < history.Length; i++)
                    history[i] = (string)key.GetValue($"BackgroundHistoryPath{i}");
            }

            _backupState = new State
            {
                Config = GetWallpaperConfig(),
                History = history,
                Wallpaper = history[0],
            };

            _historyRestored = false;
        }

        private static void Set(string filename, WallpaperStyle style)
        {
            BackupState();
            if (!TrySetWithDesktopWallpaperApi(filename, style))
            {
                SetStyle(style);
                ChangeWallpaper(filename);
            }
        }

        public static void SilentSet(string filename, WallpaperStyle style)
        {
            Set(filename, style);
            RestoreHistory();
        }
    }
}
