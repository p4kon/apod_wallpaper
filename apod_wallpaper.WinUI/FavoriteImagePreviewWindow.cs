using System;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
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
using Windows.Storage;
using Windows.Storage.Streams;
using WinRT.Interop;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSize = System.Drawing.Size;
using IOFile = System.IO.File;
using IODirectory = System.IO.Directory;
using IOPath = System.IO.Path;
using WinUIImage = Microsoft.UI.Xaml.Controls.Image;
using WinUIColor = Windows.UI.Color;

namespace apod_wallpaper.WinUI;

internal sealed class FavoriteImagePreviewWindow : Window
{
    private const double SurfaceScreenRatio = 0.74;
    private const int PreviewDecodePixelWidth = 1800;
    private const double BackdropScale = 0.11;
    private const byte BackdropTintAlpha = 96;
    private const int BackgroundFadeInMs = 100;
    private const int SurfaceFadeInMs = 150;
    private const int NativeWindowFadeInMs = 120;
    private const int CloseFadeOutMs = 80;
    private static readonly bool CapturePreviewStartupFrames = false;

    private readonly DateTime _date;
    private readonly Action<DateTime> _openInCalendar;
    private readonly string _imagePath;
    private readonly BitmapImage _previewBitmap;
    private readonly Grid _root;
    private readonly Rectangle _tintLayer;
    private readonly Border _surface;
    private readonly ScaleTransform _surfaceScale;
    private readonly IntPtr _hwnd;
    private readonly RectInt32 _workArea;
    private WndProc? _windowProc;
    private IntPtr _previousWindowProc;
    private bool _imagePrepared;
    private bool _isClosing;
    private bool _openAnimationStarted;

    public FavoriteImagePreviewWindow(string imagePath, DateTime date, Action<DateTime> openInCalendar)
    {
        _date = date.Date;
        _openInCalendar = openInCalendar;
        _imagePath = imagePath;

        ExtendsContentIntoTitleBar = true;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        _hwnd = WindowNative.GetWindowHandle(this);
        _workArea = ResolveWorkArea();
        _previewBitmap = new BitmapImage
        {
            DecodePixelWidth = PreviewDecodePixelWidth,
        };

        _surfaceScale = new ScaleTransform
        {
            ScaleX = 0.96,
            ScaleY = 0.96,
        };

        _tintLayer = BuildTintLayer();
        _surface = BuildSurface();
        _root = new Grid
        {
            Background = BuildBackdropBrush(_workArea),
            Opacity = 1,
            IsTabStop = true,
            Children =
            {
                _tintLayer,
                _surface,
            },
        };
        _root.Tapped += Root_Tapped;
        _root.KeyDown += Root_KeyDown;

        Content = _root;
        SetTitleBar(new Grid { Height = 0 });
        ConfigureWindow();
        Closed += FavoriteImagePreviewWindow_Closed;
    }

    public async Task ShowPreviewAsync()
    {
        await PreparePreviewImageAsync();
        AppWindow.MoveAndResize(CreateWarmupBounds());
        AppWindow.Show(false);
        await WaitForRenderPassesAsync(2);
        SetNativeWindowAlpha(0);
        AppWindow.MoveAndResize(_workArea);
        BringToForeground("visible");
        _root.Focus(FocusState.Programmatic);
        BeginOpenAnimation();
        await FadeInNativeWindowAsync();
    }

    private async Task PreparePreviewImageAsync()
    {
        if (_imagePrepared)
            return;

        _imagePrepared = true;
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(_imagePath);
            using IRandomAccessStream stream = await file.OpenReadAsync();
            await _previewBitmap.SetSourceAsync(stream);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Favorite preview image preload failed: {ex.Message}");
            _previewBitmap.UriSource = new Uri(_imagePath, UriKind.Absolute);
        }
    }

    private Border BuildSurface()
    {
        var image = new WinUIImage
        {
            Source = _previewBitmap,
            Stretch = Stretch.Uniform,
        };

        var calendarButton = BuildOverlayButton("\uE787", AppStrings.Get("Open favorite in Calendar"));
        calendarButton.HorizontalAlignment = HorizontalAlignment.Left;
        calendarButton.Click += CalendarButton_Click;

        var closeButton = BuildOverlayButton("\uE711", AppStrings.Get("Close preview"));
        closeButton.HorizontalAlignment = HorizontalAlignment.Right;
        closeButton.Click += async (_, _) => await ClosePreviewAsync();

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
            Opacity = 0,
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
    }

    private async void Root_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (IsWithin(_surface, e.OriginalSource as DependencyObject))
            return;

        await ClosePreviewAsync();
    }

    private async void Surface_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (IsWithinButton(e.OriginalSource as DependencyObject))
            return;

        e.Handled = true;
        await ClosePreviewAsync();
    }

    private async void CalendarButton_Click(object sender, RoutedEventArgs e)
    {
        var date = _date;
        var openInCalendar = _openInCalendar;
        await ClosePreviewAsync();
        openInCalendar(date);
    }

    private async void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        e.Handled = true;
        await ClosePreviewAsync();
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
        var tintOpacityAnimation = new DoubleAnimation
        {
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(BackgroundFadeInMs)),
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(tintOpacityAnimation, _tintLayer);
        Storyboard.SetTargetProperty(tintOpacityAnimation, "Opacity");

        var surfaceOpacityAnimation = new DoubleAnimation
        {
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(SurfaceFadeInMs)),
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(surfaceOpacityAnimation, _surface);
        Storyboard.SetTargetProperty(surfaceOpacityAnimation, "Opacity");

        var scaleXAnimation = new DoubleAnimation
        {
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(SurfaceFadeInMs)),
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(scaleXAnimation, _surfaceScale);
        Storyboard.SetTargetProperty(scaleXAnimation, "ScaleX");

        var scaleYAnimation = new DoubleAnimation
        {
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(SurfaceFadeInMs)),
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(scaleYAnimation, _surfaceScale);
        Storyboard.SetTargetProperty(scaleYAnimation, "ScaleY");

        var storyboard = new Storyboard();
        storyboard.Children.Add(tintOpacityAnimation);
        storyboard.Children.Add(surfaceOpacityAnimation);
        storyboard.Children.Add(scaleXAnimation);
        storyboard.Children.Add(scaleYAnimation);
        storyboard.Begin();
    }

    private RectInt32 CreateWarmupBounds()
    {
        return new RectInt32(
            -32000,
            -32000,
            Math.Max(1, _workArea.Width),
            Math.Max(1, _workArea.Height));
    }

    private static async Task WaitForRenderPassesAsync(int passCount)
    {
        for (var i = 0; i < passCount; i++)
            await WaitForRenderPassAsync();
    }

    private static Task WaitForRenderPassAsync()
    {
        var completion = new TaskCompletionSource<object?>();
        EventHandler<object>? rendering = null;
        rendering = (_, _) =>
        {
            CompositionTarget.Rendering -= rendering;
            completion.TrySetResult(null);
        };

        CompositionTarget.Rendering += rendering;
        _ = CompleteRenderFallbackAsync(completion, rendering);
        return completion.Task;
    }

    private static async Task CompleteRenderFallbackAsync(TaskCompletionSource<object?> completion, EventHandler<object> rendering)
    {
        await Task.Delay(120);
        if (completion.Task.IsCompleted)
            return;

        CompositionTarget.Rendering -= rendering;
        completion.TrySetResult(null);
    }

    private string? PreparePreviewStartupDiagnosticsFolder()
    {
        // Temporary startup-frame diagnostics from the fullscreen preview investigation.
        // Keep disconnected from production flow; enable only for local diagnostics.
        if (!CapturePreviewStartupFrames)
            return null;

        try
        {
            var root = IOPath.Combine(IOPath.GetTempPath(), "apod-preview-frames");
            IODirectory.CreateDirectory(root);

            var folderName = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            var folder = IOPath.Combine(root, folderName);
            IODirectory.CreateDirectory(folder);
            Debug.WriteLine($"Favorite preview diagnostics folder: {folder}");
            return folder;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Favorite preview diagnostics folder creation failed: {ex.Message}");
            return null;
        }
    }

    private void StartPreviewStartupFrameCapture(string? folder)
    {
        if (folder is null)
            return;

        _ = CapturePreviewStartupTimelineAsync(folder);
    }

    private async Task CapturePreviewStartupTimelineAsync(string folder)
    {
        await CapturePreviewStartupFrameAsync(folder, "01-after-show");
        await Task.Delay(30);
        await CapturePreviewStartupFrameAsync(folder, "04-plus-030ms");
        await Task.Delay(40);
        await CapturePreviewStartupFrameAsync(folder, "05-plus-070ms");
        await Task.Delay(50);
        await CapturePreviewStartupFrameAsync(folder, "06-plus-120ms");
        await Task.Delay(60);
        await CapturePreviewStartupFrameAsync(folder, "07-plus-180ms");
    }

    private async Task CapturePreviewStartupFrameAsync(string? folder, string frameName)
    {
        if (folder is null)
            return;

        var bounds = _workArea;
        var state = string.Join(
            Environment.NewLine,
            $"Frame={frameName}",
            $"Timestamp={DateTime.Now:O}",
            $"RootOpacity={_root.Opacity:0.###}",
            $"TintOpacity={_tintLayer.Opacity:0.###}",
            $"SurfaceOpacity={_surface.Opacity:0.###}",
            $"SurfaceScaleX={_surfaceScale.ScaleX:0.###}",
            $"SurfaceScaleY={_surfaceScale.ScaleY:0.###}",
            $"OpenAnimationStarted={_openAnimationStarted}",
            $"IsClosing={_isClosing}",
            $"WorkArea={bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}");

        try
        {
            await Task.Run(() =>
            {
                using var frame = new DrawingBitmap(bounds.Width, bounds.Height, DrawingPixelFormat.Format32bppPArgb);
                using (var graphics = DrawingGraphics.FromImage(frame))
                {
                    graphics.CopyFromScreen(
                        new DrawingPoint(bounds.X, bounds.Y),
                        DrawingPoint.Empty,
                        new DrawingSize(bounds.Width, bounds.Height));
                }

                frame.Save(IOPath.Combine(folder, $"{frameName}.png"), ImageFormat.Png);
                IOFile.WriteAllText(IOPath.Combine(folder, $"{frameName}.txt"), state);
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Favorite preview diagnostics frame '{frameName}' failed: {ex.Message}");
        }
    }

    private async Task ClosePreviewAsync()
    {
        if (_isClosing)
            return;

        _isClosing = true;
        await AnimateCloseAsync();
        Close();
    }

    private Task AnimateCloseAsync()
    {
        return FadeOutNativeWindowAsync();
    }

    private async Task FadeInNativeWindowAsync()
    {
        const int steps = 8;
        for (var step = 1; step <= steps; step++)
        {
            var alpha = (byte)Math.Clamp((int)Math.Round(255d * step / steps), 0, 255);
            SetNativeWindowAlpha(alpha);
            await Task.Delay(Math.Max(1, NativeWindowFadeInMs / steps));
        }

        RemoveNativeWindowAlpha();
    }

    private async Task FadeOutNativeWindowAsync()
    {
        const int steps = 8;
        for (var step = steps - 1; step >= 0; step--)
        {
            var alpha = (byte)Math.Clamp((int)Math.Round(255d * step / steps), 0, 255);
            SetNativeWindowAlpha(alpha);
            await Task.Delay(Math.Max(1, CloseFadeOutMs / steps));
        }
    }

    private void SetNativeWindowAlpha(byte alpha)
    {
        var extendedStyle = GetWindowLong(_hwnd, GwlExStyle);
        if ((extendedStyle & WsExLayered) == 0)
        {
            SetWindowLong(_hwnd, GwlExStyle, extendedStyle | WsExLayered);
            SetWindowPos(_hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpFrameChanged);
        }

        SetLayeredWindowAttributes(_hwnd, 0, alpha, LwaAlpha);
    }

    private void RemoveNativeWindowAlpha()
    {
        var extendedStyle = GetWindowLong(_hwnd, GwlExStyle);
        if ((extendedStyle & WsExLayered) == 0)
            return;

        SetWindowLong(_hwnd, GwlExStyle, extendedStyle & ~WsExLayered);
        SetWindowPos(_hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpFrameChanged);
    }

    private RectInt32 ResolveWorkArea()
    {
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        return displayArea.WorkArea;
    }

    private static Brush BuildBackdropBrush(RectInt32 bounds)
    {
        var backdrop = TryCreateBlurredBackdrop(bounds);
        if (backdrop != null)
        {
            return new ImageBrush
            {
                ImageSource = backdrop,
                Stretch = Stretch.Fill,
            };
        }

        return new Microsoft.UI.Xaml.Media.LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 1),
            GradientStops =
            {
                new GradientStop { Color = WinUIColor.FromArgb(255, 18, 14, 18), Offset = 0 },
                new GradientStop { Color = WinUIColor.FromArgb(255, 5, 5, 8), Offset = 1 },
            },
        };
    }

    private static Rectangle BuildTintLayer()
    {
        return new Rectangle
        {
            Fill = new SolidColorBrush(WinUIColor.FromArgb(BackdropTintAlpha, 0, 0, 0)),
            Opacity = 0,
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
        SetWindowPos(_hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize);
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

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
    private const uint SwpFrameChanged = 0x0020;
    private const uint WmMouseActivate = 0x0021;
    private const uint LwaAlpha = 0x00000002;
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
