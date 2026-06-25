using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var rootDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var appExecutablePath = Path.Combine(rootDirectory, "app", "apod_wallpaper.WinUI.exe");

        if (!File.Exists(appExecutablePath))
        {
            MessageBox.Show(
                "APOD Wallpaper cannot find app\\apod_wallpaper.WinUI.exe.\r\n\r\nPlease extract the full portable archive before starting the app.",
                "APOD Wallpaper",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 2;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = appExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(appExecutablePath),
            UseShellExecute = false
        };

        if (args.Length > 0)
            startInfo.Arguments = string.Join(" ", Array.ConvertAll(args, QuoteArgument));

        startInfo.EnvironmentVariables["APOD_WALLPAPER_PORTABLE_ROOT"] =
            rootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        try
        {
            Process.Start(startInfo);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "APOD Wallpaper failed to start.\r\n\r\n" + ex.Message,
                "APOD Wallpaper",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 3;
        }
    }

    private static string QuoteArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        if (value.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
            return value;

        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
