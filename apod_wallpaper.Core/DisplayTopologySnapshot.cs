using System;
using System.Collections.Generic;
using System.Linq;

namespace apod_wallpaper
{
    public sealed class DisplayMonitorInfo
    {
        public DisplayMonitorInfo(string devicePath, int left, int top, int right, int bottom)
        {
            DevicePath = devicePath ?? string.Empty;
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public string DevicePath { get; }
        public int Left { get; }
        public int Top { get; }
        public int Right { get; }
        public int Bottom { get; }
        public int Width => Math.Max(0, Right - Left);
        public int Height => Math.Max(0, Bottom - Top);
        public bool LooksLikePrimaryDisplay => Left == 0 && Top == 0;
    }

    public sealed class DisplayTopologySnapshot
    {
        public DisplayTopologySnapshot(IEnumerable<DisplayMonitorInfo> monitors, string errorMessage = null)
        {
            Monitors = (monitors ?? Enumerable.Empty<DisplayMonitorInfo>())
                .OrderBy(monitor => monitor.Left)
                .ThenBy(monitor => monitor.Top)
                .ToList();
            ErrorMessage = errorMessage;
        }

        public IReadOnlyList<DisplayMonitorInfo> Monitors { get; }
        public bool IsAvailable => string.IsNullOrWhiteSpace(ErrorMessage);
        public bool IsMultiMonitor => Monitors.Count > 1;
        public string ErrorMessage { get; }
    }

    public sealed class DisplayTopologyService
    {
        public DisplayTopologySnapshot Capture()
        {
            return WallpaperNative.GetDisplayTopologySnapshot();
        }
    }
}
