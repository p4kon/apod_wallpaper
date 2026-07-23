using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace apod_wallpaper.WinUI;

internal static class UpdateNotificationDialog
{
    public static async Task<UpdateDialogChoice> ShowAsync(XamlRoot xamlRoot, apod_wallpaper.UpdateCheckResult result, bool includeDoNotRemind)
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(new TextBlock
        {
            Text = AppStrings.Format("Version {0} is available.", result.LatestVersion),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        stack.Children.Add(new TextBlock
        {
            Text = AppStrings.Format("You are running version {0}.", result.CurrentVersion),
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        });
        stack.Children.Add(new TextBlock
        {
            Text = AppStrings.Get("APOD Wallpaper will open the GitHub release page. It will not install updates automatically."),
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        });

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = AppStrings.Get("Update available"),
            PrimaryButtonText = AppStrings.Get("Open GitHub release"),
            CloseButtonText = AppStrings.Get("Later"),
            DefaultButton = ContentDialogButton.Primary,
            Content = stack,
        };

        if (includeDoNotRemind)
            dialog.SecondaryButtonText = AppStrings.Get("Do not remind");

        var dialogResult = await dialog.ShowAsync();
        if (dialogResult == ContentDialogResult.Primary)
            return UpdateDialogChoice.OpenRelease;
        if (dialogResult == ContentDialogResult.Secondary && includeDoNotRemind)
            return UpdateDialogChoice.DoNotRemind;

        return UpdateDialogChoice.Later;
    }
}
