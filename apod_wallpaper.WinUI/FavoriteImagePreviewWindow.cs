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
using WinRT.Interop;

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
    private readonly IntPtr _hwnd;
    private bool _openAnimationStarted;

    public FavoriteImagePreviewWindow(string imagePath, DateTime date, Action<DateTime> openInCalendar)
    {
        _date = date.Date;
        _openInCalendar = openInCalendar;

        ExtendsContentIntoTitleBar = true;
        SystemBackdrop = new DesktopAcrylicBackdrop();
        AppWindow.SetIcon("Assets/AppIcon.ico");
        _hwnd = WindowNative.GetWindowHandle(this);

        _surfaceScale = new ScaleTransform
        {
            ScaleX = 0.96,
            ScaleY = 0.96,
        };

        _surface = BuildSurface(imagePath);
        _root = new Grid
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(46, 0, 0, 0)),
            Opacity = 1,
            Children = { _surface },
        };
        _root.Tapped += Root_Tapped;
        _root.Loaded += (_, _) => BeginOpenAnimation();

        Content = _root;
        SetTitleBar(new Grid { Height = 0 });
        ConfigureWindow();
    }

    public void ShowPreview()
    {
        Activate();
        SetForegroundWindow(_hwnd);
        DispatcherQueue.TryEnqueue(BeginOpenAnimation);
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

        var closeButton = BuildOverlayButton("\uE711", AppStrings.Get("Close preview"));
        closeButton.HorizontalAlignment = HorizontalAlignment.Right;
        closeButton.Click += (_, _) => Close();

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
        surface.Tapped += Surface_Tapped;
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
            presenter.IsAlwaysOnTop = true;
        }

        EnableLayeredComposition(_hwnd);
        AppWindow.MoveAndResize(new RectInt32(workArea.X, workArea.Y, workArea.Width, workArea.Height));
        Activated += FavoriteImagePreviewWindow_Activated;
    }

    private void FavoriteImagePreviewWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
            return;

        Activated -= FavoriteImagePreviewWindow_Activated;
        BeginOpenAnimation();
    }

    private void Root_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (IsWithin(_surface, e.OriginalSource as DependencyObject))
            return;

        Close();
    }

    private void Surface_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (IsWithinButton(e.OriginalSource as DependencyObject))
            return;

        Close();
        e.Handled = true;
    }

    private void CalendarButton_Click(object sender, RoutedEventArgs e)
    {
        var date = _date;
        var openInCalendar = _openInCalendar;
        Close();
        openInCalendar(date);
    }

    private static bool IsWithin(DependencyObject parent, DependencyObject? child)
    {
        while (child != null)
        {
            if (ReferenceEquals(parent, child))
                return true;

            child = VisualTreeHelper.GetParent(child);
        }

        return false;
    }

    private static bool IsWithinButton(DependencyObject? child)
    {
        while (child != null)
        {
            if (child is Button)
                return true;

            child = VisualTreeHelper.GetParent(child);
        }

        return false;
    }

    private void BeginOpenAnimation()
    {
        if (_openAnimationStarted)
            return;

        _openAnimationStarted = true;
        AnimateOpen();
    }

    private void AnimateOpen()
    {
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
        storyboard.Children.Add(scaleXAnimation);
        storyboard.Children.Add(scaleYAnimation);
        storyboard.Begin();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    private static void EnableLayeredComposition(IntPtr hwnd)
    {
        var extendedStyle = GetWindowLong(hwnd, GwlExStyle);
        if (extendedStyle == 0)
            return;

        SetWindowLong(hwnd, GwlExStyle, extendedStyle | WsExLayered);
        SetLayeredWindowAttributes(hwnd, 0, 255, LwaAlpha);
    }

    private const int GwlExStyle = -20;
    private const int WsExLayered = 0x00080000;
    private const int LwaAlpha = 0x00000002;

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint colorKey, byte alpha, uint flags);
}
