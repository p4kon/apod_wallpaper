using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace apod_wallpaper.WinUI;

internal static class LocalizationHelper
{
    public static void ApplyTo(DependencyObject root)
    {
        foreach (var element in Enumerate(root))
        {
            if (element is TextBlock textBlock)
                textBlock.Text = AppStrings.Get(textBlock.Text);

            if (element is TextBox textBox)
                textBox.PlaceholderText = AppStrings.Get(textBox.PlaceholderText);

            if (element is InfoBar infoBar)
            {
                infoBar.Title = AppStrings.Get(infoBar.Title);
                infoBar.Message = AppStrings.Get(infoBar.Message);
            }

            if (element is ToggleSwitch toggleSwitch)
            {
                if (toggleSwitch.Header is string header)
                    toggleSwitch.Header = AppStrings.Get(header);
                if (toggleSwitch.OnContent is string onContent)
                    toggleSwitch.OnContent = AppStrings.Get(onContent);
                if (toggleSwitch.OffContent is string offContent)
                    toggleSwitch.OffContent = AppStrings.Get(offContent);
            }

            if (element is ContentControl contentControl && contentControl.Content is string content)
                contentControl.Content = AppStrings.Get(content);

            if (element is MenuFlyoutItem menuFlyoutItem)
                menuFlyoutItem.Text = AppStrings.Get(menuFlyoutItem.Text);
        }
    }

    private static IEnumerable<DependencyObject> Enumerate(DependencyObject root)
    {
        yield return root;

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            foreach (var child in Enumerate(VisualTreeHelper.GetChild(root, i)))
                yield return child;
        }
    }
}
