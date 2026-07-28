using System;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
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
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;
using WinRT.Interop;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingBrush = System.Drawing.SolidBrush;
using DrawingColor = System.Drawing.Color;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSize = System.Drawing.Size;
using WinUIImage = Microsoft.UI.Xaml.Controls.Image;
using WinUIColor = Windows.UI.Color;

namespace apod_wallpaper.WinUI;

internal sealed class FavoriteImagePreviewWindow : Window
{
    private const double SurfaceScreenRatio = 0.74;
    private const int PreviewDecodePixelWidth = 1800;
    private const double BackdropScale = 0.11;
    private const byte BackdropTintAlpha = 96;

    private readonly DateTime _date;
    private readonly Action<DateTime> _openInCalendar;
    private readonly Grid _root;
    private readonly Border _surface;
    private readonly ScaleTransform _surfaceScale;
    private readonly IntPtr _hwnd;
    private readonly RectInt32 _workArea;
    private WndProc? _windowProc;
    private IntPtr _previousWindowProc;
    private bool _openAnimationStarted;

    public FavoriteImagePreviewWindow(string imagePath, DateTime date, Action<DateTime> openInCalendar)
    {
        _date = date.Date;
        _openInCalendar = openInCalendar;

        ExtendsContentIntoTitleBar = true;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        _hwnd = WindowNative.GetWindowHandle(this);
        _workArea = ResolveWorkArea();

        _surfaceScale = new ScaleTransform
        {
            ScaleX = 0.96,
            ScaleY = 0.96,
        };

        _surface = BuildSurface(imagePath);
        _root = new Grid
        {
            Background = new SolidColorBrush(WinUIColor.FromArgb(255, 14, 11, 12)),
            Opacity = 1,
            Children =
            {
                BuildBackdropLayer(_workArea),
                BuildTintLayer(),
                _surface,
            },
        };
        _root.Tapped += Root_Tapped;
        _root.Loaded += (_, _) => BeginOpenAnimation();

        Content = _root;
        SetTitleBar(new Grid { Height = 0 });
        ConfigureWindow();
        Closed += FavoriteImagePreviewWindow_Closed;
    }

    public void ShowPreview()
    {
        Activate();
        BringToForeground("initial");
        DispatcherQueue.TryEnqueue(() =>
        {
            Activate();
            BringToForeground("deferred");
            BeginOpenAnimation();
        });
    }

    private Border BuildSurface(string imagePath)
    {
        var image = new WinUIImage
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
            Background = new SolidColorBrush(WinUIColor.FromArgb(255, 0, 0, 0)),
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
        _surface.MaxWidth = Math.Max(420, _workArea.Width * SurfaceScreenRatio);
        _surface.MaxHeight = Math.Max(320, _workArea.Height * SurfaceScreenRatio);

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        ConfigureNativeWindowChrome(_hwnd);
        InstallMouseActivateGuard(_hwnd);
        AppWindow.MoveAndResize(_workArea);
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

    private void FavoriteImagePreviewWindow_Closed(object sender, WindowEventArgs args)
    {
        RemoveMouseActivateGuard();
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

    private RectInt32 ResolveWorkArea()
    {
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        return displayArea.WorkArea;
    }

    private UIElement BuildBackdropLayer(RectInt32 bounds)
    {
        var backdrop = TryCreateBlurredBackdrop(bounds);
        if (backdrop != null)
        {
            return new WinUIImage
            {
                Source = backdrop,
                Stretch = Stretch.Fill,
                IsHitTestVisible = false,
            };
        }

        return new Grid
        {
            Background = new Microsoft.UI.Xaml.Media.LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = WinUIColor.FromArgb(255, 18, 14, 18), Offset = 0 },
                    new GradientStop { Color = WinUIColor.FromArgb(255, 5, 5, 8), Offset = 1 },
                },
            },
            IsHitTestVisible = false,
        };
    }

    private static Rectangle BuildTintLayer()
    {
        return new Rectangle
        {
            Fill = new SolidColorBrush(WinUIColor.FromArgb(BackdropTintAlpha, 0, 0, 0)),
            IsHitTestVisible = false,
        };
    }

    private static ImageSource? TryCreateBlurredBackdrop(RectInt32 bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return null;

        try
        {
            using var captured = new DrawingBitmap(bounds.Width, bounds.Height, DrawingPixelFormat.Format32bppPArgb);
            using (var graphics = DrawingGraphics.FromImage(captured))
            {
                graphics.CopyFromScreen(
                    new DrawingPoint(bounds.X, bounds.Y),
                    DrawingPoint.Empty,
                    new DrawingSize(bounds.Width, bounds.Height));
            }

            var smallWidth = Math.Max(96, (int)Math.Round(bounds.Width * BackdropScale));
            var smallHeight = Math.Max(54, (int)Math.Round(bounds.Height * BackdropScale));

            using var small = new DrawingBitmap(smallWidth, smallHeight, DrawingPixelFormat.Format32bppPArgb);
            using (var graphics = DrawingGraphics.FromImage(small))
            {
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighSpeed;
                graphics.DrawImage(captured, new DrawingRectangle(0, 0, smallWidth, smallHeight));
            }

            using var blurred = new DrawingBitmap(bounds.Width, bounds.Height, DrawingPixelFormat.Format32bppPArgb);
            using (var graphics = DrawingGraphics.FromImage(blurred))
            {
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighSpeed;
                graphics.DrawImage(small, new DrawingRectangle(0, 0, bounds.Width, bounds.Height));
                using var tint = new DrawingBrush(DrawingColor.FromArgb(104, 0, 0, 0));
                graphics.FillRectangle(tint, new DrawingRectangle(0, 0, bounds.Width, bounds.Height));
            }

            return CreateImageSource(blurred);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Favorite preview backdrop capture failed: {ex.Message}");
            return null;
        }
    }

    private static WriteableBitmap CreateImageSource(DrawingBitmap bitmap)
    {
        var source = new WriteableBitmap(bitmap.Width, bitmap.Height);
        var rectangle = new DrawingRectangle(0, 0, bitmap.Width, bitmap.Height);
        var bitmapData = bitmap.LockBits(rectangle, ImageLockMode.ReadOnly, DrawingPixelFormat.Format32bppPArgb);
        try
        {
            using var pixelStream = source.PixelBuffer.AsStream();
            var stride = Math.Abs(bitmapData.Stride);
            var row = new byte[stride];

            for (var y = 0; y < bitmap.Height; y++)
            {
                var rowStart = bitmapData.Stride >= 0
                    ? bitmapData.Scan0 + (y * bitmapData.Stride)
                    : bitmapData.Scan0 + ((bitmap.Height - 1 - y) * stride);
                Marshal.Copy(rowStart, row, 0, stride);
                pixelStream.Write(row, 0, bitmap.Width * 4);
            }
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        source.Invalidate();
        return source;
    }

    private void BringToForeground(string phase)
    {
        SetWindowPos(_hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpShowWindow);
        var foregroundSet = SetForegroundWindow(_hwnd);
        var isForeground = GetForegroundWindow() == _hwnd;
        Debug.WriteLine($"Favorite preview foreground pass ({phase}): SetForegroundWindow={foregroundSet}, IsForeground={isForeground}");
    }

    private static void ConfigureNativeWindowChrome(IntPtr hwnd)
    {
        var style = GetWindowLong(hwnd, GwlStyle);
        style &= ~(WsCaption | WsThickFrame | WsBorder | WsDlgFrame);
        SetWindowLong(hwnd, GwlStyle, style);

        var extendedStyle = GetWindowLong(hwnd, GwlExStyle);
        extendedStyle &= ~(WsExLayered | WsExNoActivate | WsExTransparent);
        SetWindowLong(hwnd, GwlExStyle, extendedStyle);

        SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpFrameChanged);
        DisableDwmEdges(hwnd);
    }

    private static void DisableDwmEdges(IntPtr hwnd)
    {
        var cornerPreference = DwmWindowCornerPreferenceDoNotRound;
        _ = DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref cornerPreference, sizeof(int));

        var borderColor = DwmColorNone;
        _ = DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref borderColor, sizeof(uint));
    }

    private void InstallMouseActivateGuard(IntPtr hwnd)
    {
        _windowProc = PreviewWindowProc;
        _previousWindowProc = SetWindowLongPtr(hwnd, GwlpWndProc, Marshal.GetFunctionPointerForDelegate(_windowProc));
        if (_previousWindowProc == IntPtr.Zero)
            Debug.WriteLine($"Favorite preview subclass install failed: {Marshal.GetLastWin32Error()}");
    }

    private void RemoveMouseActivateGuard()
    {
        if (_previousWindowProc == IntPtr.Zero)
            return;

        SetWindowLongPtr(_hwnd, GwlpWndProc, _previousWindowProc);
        _previousWindowProc = IntPtr.Zero;
        _windowProc = null;
    }

    private IntPtr PreviewWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == WmMouseActivate)
            return new IntPtr(MaActivate);

        return _previousWindowProc == IntPtr.Zero
            ? DefWindowProc(hwnd, message, wParam, lParam)
            : CallWindowProc(_previousWindowProc, hwnd, message, wParam, lParam);
    }

    private static IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value)
    {
        if (IntPtr.Size == 8)
            return SetWindowLongPtr64(hwnd, index, value);

        return new IntPtr(SetWindowLong32(hwnd, index, value.ToInt32()));
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hwnd, int index, int value);

    [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
    private static extern IntPtr CallWindowProc(IntPtr previousWindowProc, IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref uint value, int size);

    private delegate IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    private static readonly IntPtr HwndTopmost = new(-1);

    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const int GwlpWndProc = -4;
    private const int WsCaption = 0x00C00000;
    private const int WsThickFrame = 0x00040000;
    private const int WsBorder = 0x00800000;
    private const int WsDlgFrame = 0x00400000;
    private const int WsExLayered = 0x00080000;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExTransparent = 0x00000020;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpFrameChanged = 0x0020;
    private const uint WmMouseActivate = 0x0021;
    private const int MaActivate = 1;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmWindowCornerPreferenceDoNotRound = 1;
    private const uint DwmColorNone = 0xFFFFFFFE;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}
