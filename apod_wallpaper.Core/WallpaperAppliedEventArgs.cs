using System;

namespace apod_wallpaper
{
    public sealed class WallpaperAppliedEventArgs : EventArgs
    {
        public WallpaperAppliedEventArgs(ApodWorkflowResult result, bool automatic)
        {
            Result = result;
            Automatic = automatic;
        }

        public ApodWorkflowResult Result { get; }
        public bool Automatic { get; }
    }
}
