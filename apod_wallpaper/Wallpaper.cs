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
    }

    internal static class WallpaperNative
    {
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
                default:
                    throw new ArgumentOutOfRangeException(nameof(style));
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
            SetStyle(style);
            ChangeWallpaper(filename);
        }

        public static void SilentSet(string filename, WallpaperStyle style)
        {
            Set(filename, style);
            RestoreHistory();
        }
    }
}
