using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace apod_wallpaper.WinUI;

public sealed partial class ShellPage : Page
{
    private ShellPageArguments? _arguments;
    private static readonly SolidColorBrush ActiveNavBrush = new(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x00, 0x78, 0xD4));
    private static readonly SolidColorBrush InactiveNavBrush = new(Microsoft.UI.Colors.Transparent);

    public ShellPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        _arguments = e.Parameter as ShellPageArguments;
        if (_arguments == null)
            return;

        NavigateToPreview();
    }

    private void PreviewButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToPreview();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSettings();
    }

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToAbout();
    }

    private void HideToTrayButton_Click(object sender, RoutedEventArgs e)
    {
        _arguments?.HideWindowToTray();
    }

    private void NavigateToPreview()
    {
        if (_arguments == null)
            return;

        SetActiveButton(PreviewButton);
        ContentFrame.Navigate(typeof(MainPage), _arguments.CreateMainPageArguments());
    }

    private void NavigateToSettings()
    {
        if (_arguments == null)
            return;

        SetActiveButton(SettingsButton);
        ContentFrame.Navigate(typeof(SettingsPage), _arguments.CreateSettingsPageArguments());
    }

    private void NavigateToAbout()
    {
        SetActiveButton(AboutButton);
        ContentFrame.Navigate(typeof(AboutPage));
    }

    private void SetActiveButton(Button activeButton)
    {
        SetButtonState(PreviewButton, activeButton == PreviewButton);
        SetButtonState(SettingsButton, activeButton == SettingsButton);
        SetButtonState(AboutButton, activeButton == AboutButton);
    }

    private static void SetButtonState(Button button, bool isActive)
    {
        button.Background = isActive ? ActiveNavBrush : InactiveNavBrush;
        button.Foreground = isActive
            ? new SolidColorBrush(Microsoft.UI.Colors.White)
            : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
    }
}
