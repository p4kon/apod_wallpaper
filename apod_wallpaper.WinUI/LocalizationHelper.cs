using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Automation;
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
            var automationName = AutomationProperties.GetName(element);
            if (!string.IsNullOrEmpty(automationName))
                AutomationProperties.SetName(element, Translate(element, "AutomationProperties.Name", automationName));

            var automationHelpText = AutomationProperties.GetHelpText(element);
            if (!string.IsNullOrEmpty(automationHelpText))
                AutomationProperties.SetHelpText(element, Translate(element, "AutomationProperties.HelpText", automationHelpText));

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
        var visited = new HashSet<DependencyObject>();
        foreach (var element in Enumerate(root, visited))
            yield return element;
    }

    private static IEnumerable<DependencyObject> Enumerate(DependencyObject root, HashSet<DependencyObject> visited)
    {
        if (!visited.Add(root))
            yield break;

        yield return root;

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            foreach (var child in Enumerate(VisualTreeHelper.GetChild(root, i), visited))
                yield return child;
        }

        foreach (var child in EnumerateLogicalChildren(root))
        {
            foreach (var descendant in Enumerate(child, visited))
                yield return descendant;
        }
    }

    private static IEnumerable<DependencyObject> EnumerateLogicalChildren(DependencyObject root)
    {
        if (root is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is DependencyObject dependencyObject)
                    yield return dependencyObject;
            }
        }

        if (root is Border border && border.Child is DependencyObject borderChild)
            yield return borderChild;

        if (root is ScrollViewer scrollViewer && scrollViewer.Content is DependencyObject scrollContent)
            yield return scrollContent;

        if (root is ContentControl contentControl && contentControl.Content is DependencyObject contentChild)
            yield return contentChild;

        if (root is ItemsControl itemsControl)
        {
            foreach (var item in itemsControl.Items)
            {
                if (item is DependencyObject dependencyObject)
                    yield return dependencyObject;
            }
        }
    }
}
