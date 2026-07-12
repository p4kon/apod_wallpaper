using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace apod_wallpaper.WinUI;

internal static class LocalizationHelper
{
    private static readonly DependencyProperty LocalizationKeysProperty =
        DependencyProperty.RegisterAttached(
            "LocalizationKeys",
            typeof(Dictionary<string, string>),
            typeof(LocalizationHelper),
            new PropertyMetadata(null));

    public static void ApplyTo(DependencyObject root)
    {
        foreach (var element in Enumerate(root))
        {
            if (element is TextBlock textBlock)
                textBlock.Text = Translate(textBlock, nameof(TextBlock.Text), textBlock.Text);

            if (element is TextBox textBox)
                textBox.PlaceholderText = Translate(textBox, nameof(TextBox.PlaceholderText), textBox.PlaceholderText);

            if (element is InfoBar infoBar)
            {
                infoBar.Title = Translate(infoBar, nameof(InfoBar.Title), infoBar.Title);
                infoBar.Message = Translate(infoBar, nameof(InfoBar.Message), infoBar.Message);
            }

            if (element is ToggleSwitch toggleSwitch)
            {
                if (toggleSwitch.Header is string header)
                    toggleSwitch.Header = Translate(toggleSwitch, nameof(ToggleSwitch.Header), header);
                if (toggleSwitch.OnContent is string onContent)
                    toggleSwitch.OnContent = Translate(toggleSwitch, nameof(ToggleSwitch.OnContent), onContent);
                if (toggleSwitch.OffContent is string offContent)
                    toggleSwitch.OffContent = Translate(toggleSwitch, nameof(ToggleSwitch.OffContent), offContent);
            }

            if (element is ContentControl contentControl && contentControl.Content is string content)
                contentControl.Content = Translate(contentControl, nameof(ContentControl.Content), content);

            if (element is ItemsControl itemsControl)
            {
                foreach (var item in itemsControl.Items)
                {
                    if (item is ContentControl itemContentControl && itemContentControl.Content is string itemContent)
                        itemContentControl.Content = Translate(itemContentControl, nameof(ContentControl.Content), itemContent);
                }
            }

            if (element is MenuFlyoutItem menuFlyoutItem)
                menuFlyoutItem.Text = Translate(menuFlyoutItem, nameof(MenuFlyoutItem.Text), menuFlyoutItem.Text);
        }
    }

    private static string Translate(DependencyObject owner, string propertyName, string? currentValue)
    {
        if (string.IsNullOrEmpty(currentValue))
            return currentValue ?? string.Empty;

        var keys = (Dictionary<string, string>?)owner.GetValue(LocalizationKeysProperty);
        if (keys == null)
        {
            keys = new Dictionary<string, string>();
            owner.SetValue(LocalizationKeysProperty, keys);
        }

        if (!keys.TryGetValue(propertyName, out var key))
        {
            key = AppStrings.GetStableKey(currentValue);
            keys[propertyName] = key;
        }
        else
        {
            var stableKey = AppStrings.GetStableKey(key);
            if (!string.Equals(stableKey, key, StringComparison.Ordinal))
            {
                key = stableKey;
                keys[propertyName] = key;
            }
        }

        return AppStrings.Get(key);
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
