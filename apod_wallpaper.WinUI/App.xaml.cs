using Microsoft.UI.Xaml;

namespace apod_wallpaper.WinUI;

public partial class App : Application
{
    private MainWindow? _window;

    internal BackendHost? Host { get; private set; }

    internal MainWindow? MainWindow => _window;

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
