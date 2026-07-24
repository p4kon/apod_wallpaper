using System;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;

namespace apod_wallpaper.WinUI;

internal sealed class FavoriteImagePreviewWindow : Window
{
    private const double SurfaceScreenRatio = 0.74;
    private const int PreviewDecodePixelWidth = 1800;

    private readonly DateTime _date;
    private readonly Action<DateTime> _openInCalendar;
    private readonly Grid _root;
    private readonly Border _surface;
    private readonly ScaleTransform _surfaceScale;

    public FavoriteImagePreviewWindow(string imagePath, DateTime date, Action<DateTime> openInCalendar)
    {
        _date = date.Date;
        _openInCalendar = openInCalendar;

        ExtendsContentIntoTitleBar = true;
        AppWindow.SetIcon("Assets/AppIcon.ico");

        _surfaceScale = new ScaleTransform
        {
            ScaleX = 0.92,
            ScaleY = 0.92,
        };

        _surface = BuildSurface(imagePath);
        _root = new Grid
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(226, 0, 0, 0)),
            Opacity = 0,
            Children = { _surface },
        };
        _root.Tapped += Root_Tapped;

        Content = _root;
        SetTitleBar(new Grid { Height = 0 });
        ConfigureWindow();
    }

    private Border BuildSurface(string imagePath)
    {
        var image = new Image
        {
            Source = new BitmapImage
            {
                DecodePixelWidth = PreviewDecodePixelWidth,
                UriSource = new Uri(imagePath, UriKind.Absolute),
            },
            Stretch = Stretch.Uniform,
        };

        var calendarButton = BuildOverlayButton("\uE787", AppStrings.Get("Open favorite in Calendar"));
        calendarButton.HorizontalAlignment = HorizontalAlignment.Left;
        calendarButton.Click += CalendarButton_Click;
        calendarButton.Tapped += CommandButton_Tapped;

        var closeButton = BuildOverlayButton("\uE711", AppStrings.Get("Close preview"));
        closeButton.HorizontalAlignment = HorizontalAlignment.Right;
        closeButton.Click += (_, _) => Close();
        closeButton.Tapped += CommandButton_Tapped;

        var grid = new Grid
        {
            Children =
            {
                image,
                calendarButton,
                closeButton,
            },
        };

        var surface = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0)),
            Child = grid,
            CornerRadius = new CornerRadius(14),
            RenderTransform = _surfaceScale,
            RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
        };
        surface.Tapped += (_, _) => Close();
        return surface;
    }

    private static Button BuildOverlayButton(string glyph, string name)
    {
        var button = new Button
        {
            Width = 38,
            Height = 38,
            Margin = new Thickness(16),
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Top,
            Content = new FontIcon
            {
                Glyph = glyph,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
            },
        };
        ToolTipService.SetToolTip(button, name);
        AutomationProperties.SetName(button, name);
        return button;
    }

    private void ConfigureWindow()
    {
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;
        _surface.MaxWidth = Math.Max(420, workArea.Width * SurfaceScreenRatio);
        _surface.MaxHeight = Math.Max(320, workArea.Height * SurfaceScreenRatio);

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }

        AppWindow.MoveAndResize(new RectInt32(workArea.X, workArea.Y, workArea.Width, workArea.Height));
        AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        Activated += FavoriteImagePreviewWindow_Activated;
    }

    private void FavoriteImagePreviewWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
            return;

        Activated -= FavoriteImagePreviewWindow_Activated;
        AnimateOpen();
    }

    private void Root_Tapped(object sender, TappedRoutedEventArgs e)
    {
        Close();
    }

    private void CalendarButton_Click(object sender, RoutedEventArgs e)
    {
        _openInCalendar(_date);
        Close();
    }

    private void CommandButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
    }

    private void AnimateOpen()
    {
        var opacityAnimation = new DoubleAnimation
        {
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(opacityAnimation, _root);
        Storyboard.SetTargetProperty(opacityAnimation, "Opacity");

        var scaleXAnimation = new DoubleAnimation
        {
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(scaleXAnimation, _surfaceScale);
        Storyboard.SetTargetProperty(scaleXAnimation, "ScaleX");

        var scaleYAnimation = new DoubleAnimation
        {
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(scaleYAnimation, _surfaceScale);
        Storyboard.SetTargetProperty(scaleYAnimation, "ScaleY");

        var storyboard = new Storyboard();
        storyboard.Children.Add(opacityAnimation);
        storyboard.Children.Add(scaleXAnimation);
        storyboard.Children.Add(scaleYAnimation);
        storyboard.Begin();
    }
}
