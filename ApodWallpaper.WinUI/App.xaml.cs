using Microsoft.UI.Xaml;

namespace ApodWallpaper.WinUI;

public partial class App : Application
{
    private Window? _window;

    internal BackendHost? Host { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Host = new BackendHost();
        var initialization = await Host.InitializeAsync();

        _window = new MainWindow(Host, initialization);
        _window.Activate();
    }
}
