using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace apod_wallpaper.WinUI;

public sealed partial class ShellPage : Page
{
    private ShellPageArguments? _arguments;

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

        AppNavigationView.SelectedItem = PreviewNavItem;
        NavigateToPreview();
    }

    private void AppNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is not NavigationViewItem selectedItem)
            return;

        NavigateByTag(selectedItem.Tag as string);
    }

    private void PreviewButton_Click(object sender, RoutedEventArgs e)
    {
        AppNavigationView.SelectedItem = PreviewNavItem;
        NavigateToPreview();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        AppNavigationView.SelectedItem = SettingsNavItem;
        NavigateToSettings();
    }

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        AppNavigationView.SelectedItem = AboutNavItem;
        NavigateToAbout();
    }

    private void HideToTrayButton_Click(object sender, RoutedEventArgs e)
    {
        _arguments?.HideWindowToTray();
    }

    private void NavigateByTag(string? tag)
    {
        switch (tag)
        {
            case "settings":
                NavigateToSettings();
                break;
            case "about":
                NavigateToAbout();
                break;
            default:
                NavigateToPreview();
                break;
        }
    }

    private void NavigateToPreview()
    {
        if (_arguments == null)
            return;

        HostBadgeText.Text = "Preview workspace";
        ContentFrame.Navigate(typeof(MainPage), _arguments.CreateMainPageArguments());
    }

    private void NavigateToSettings()
    {
        HostBadgeText.Text = "Settings placeholder";
        ContentFrame.Navigate(typeof(SettingsPage));
    }

    private void NavigateToAbout()
    {
        HostBadgeText.Text = "About this host";
        ContentFrame.Navigate(typeof(AboutPage));
    }
}
