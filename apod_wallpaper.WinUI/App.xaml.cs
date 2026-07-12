using Microsoft.UI.Xaml;
using System;
using System.IO;

namespace apod_wallpaper.WinUI;

public partial class App : Application
{
    private MainWindow? _window;

    internal BackendHost? Host { get; private set; }

    internal MainWindow? MainWindow => _window;

    public App()
    {
        PortableStartupLog.Write("App constructor started.");
        InitializeComponent();
        PortableStartupLog.Write("App constructor completed.");
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            PortableStartupLog.Write("OnLaunched started.");
            Host = new BackendHost();
            PortableStartupLog.Write("BackendHost created.");
            var initialization = await Host.InitializeAsync();
            PortableStartupLog.Write("Backend initialized. Succeeded=" + initialization.Succeeded);
            if (initialization.Succeeded && initialization.Value != null)
                AppStrings.ApplyLanguage(initialization.Value.Language);

            _window = new MainWindow(Host, initialization);
            PortableStartupLog.Write("MainWindow created.");
            _window.Activate();
            PortableStartupLog.Write("MainWindow activated.");
        }
        catch (Exception ex)
        {
            PortableStartupLog.Write("Startup failed: " + ex);
            throw;
        }
    }
}

internal static class PortableStartupLog
{
    public static void Write(string message)
    {
        try
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "startup-logs");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "startup.log");
            File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + Environment.NewLine);
        }
        catch
        {
        }
    }
}
