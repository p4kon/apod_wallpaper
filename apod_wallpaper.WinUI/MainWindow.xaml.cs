using Microsoft.UI.Xaml;

namespace apod_wallpaper.WinUI;

public sealed partial class MainWindow : Window
{
    internal MainWindow(
        BackendHost backendHost,
        apod_wallpaper.OperationResult<apod_wallpaper.ApplicationSettingsSnapshot> initialization)
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        RootFrame.Navigate(typeof(MainPage), new MainPageArguments(backendHost, initialization));
    }
}
