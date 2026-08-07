using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Input;
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
using Windows.System;
using Windows.UI.Core;
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
    private const double MinZoom = 0.4;
    private const double MaxZoom = 2.0;
    private const double WheelZoomStep = 0.12;
    private const double DragThreshold = 6;
    private const int SingleClickCloseDelayMs = 260;
    private static readonly bool CapturePreviewStartupFrames = false;

    private readonly FavoriteImageActions _actions;
    private readonly List<apod_wallpaper.FavoriteApodItem> _items;
    private readonly Grid _root;
    private readonly Rectangle _tintLayer;
    private readonly Grid _surface;
    private readonly Grid _imageViewport;
    private readonly WinUIImage _previewImage;
    private readonly CompositeTransform _imageTransform;
    private readonly ScaleTransform _surfaceScale;
    private readonly Border _infoPanel;
    private readonly TextBlock _infoText;
    private readonly DispatcherTimer _infoPanelTimer = new();
    private readonly IntPtr _hwnd;
    private readonly RectInt32 _workArea;
    private WndProc? _windowProc;
    private IntPtr _previousWindowProc;
    private BitmapImage? _previewBitmap;
    private int _currentIndex;
    private double _zoom = 1;
    private double _panX;
    private double _panY;
    private double _startPanX;
    private double _startPanY;
    private Windows.Foundation.Point _pressPoint;
    private Windows.Foundation.Point _lastClickPoint;
    private DateTime _lastClickUtc = DateTime.MinValue;
    private CancellationTokenSource? _singleClickCloseCts;
    private bool _pointerPressed;
    private bool _pointerStartedOnImage;
    private bool _isDragging;
    private bool _isClosing;
    private bool _openAnimationStarted;

    public FavoriteImagePreviewWindow(
        IReadOnlyList<apod_wallpaper.FavoriteApodItem> items,
        DateTime initialDate,
        FavoriteImageActions actions)
    {
        _items = items
            .Where(item => !string.IsNullOrWhiteSpace(item.ImagePath) && IOFile.Exists(item.ImagePath))
            .OrderByDescending(item => item.Date)
            .ToList();
        _currentIndex = Math.Max(0, _items.FindIndex(item => item.Date.Date == initialDate.Date));
        _actions = actions;

        ExtendsContentIntoTitleBar = true;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        _hwnd = WindowNative.GetWindowHandle(this);
        _workArea = ResolveWorkArea();

        _surfaceScale = new ScaleTransform
        {
            ScaleX = 0.96,
            ScaleY = 0.96,
        };
        _imageTransform = new CompositeTransform();
        _previewImage = new WinUIImage
        {
            Stretch = Stretch.Uniform,
        };

        _imageViewport = BuildImageViewport();
        _imageViewport.RenderTransform = _imageTransform;
        _imageViewport.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        _tintLayer = BuildTintLayer();
        _surface = BuildSurface();
        _infoText = BuildInfoText();
        _infoPanel = BuildInfoPanel(_infoText);
        _infoPanel.PointerPressed += InfoPanel_PointerPressed;
        _infoPanel.PointerReleased += InfoPanel_PointerReleased;
        _root = new Grid
        {
            Background = BuildBackdropBrush(_workArea),
            Opacity = 1,
            IsTabStop = true,
            Children =
            {
                _tintLayer,
                _surface,
                _infoPanel,
            },
        };
        _root.PointerPressed += Root_PointerPressed;
        _root.PointerMoved += Root_PointerMoved;
        _root.PointerReleased += Root_PointerReleased;
        _root.PointerCanceled += Root_PointerCanceled;
        _root.PointerCaptureLost += Root_PointerCaptureLost;
        _root.PointerWheelChanged += Root_PointerWheelChanged;
        _root.KeyDown += Root_KeyDown;
        _root.SizeChanged += Root_SizeChanged;

        _infoPanelTimer.Interval = TimeSpan.FromSeconds(3);
        _infoPanelTimer.Tick += InfoPanelTimer_Tick;

        Content = _root;
        SetTitleBar(new Grid { Height = 0 });
        ConfigureWindow();
        Closed += FavoriteImagePreviewWindow_Closed;
    }

    public async Task ShowPreviewAsync()
    {
        await ShowCurrentItemAsync();
        AppWindow.MoveAndResize(CreateWarmupBounds());
        AppWindow.Show(false);
        await WaitForRenderPassesAsync(2);
        SetNativeWindowAlpha(0);
        AppWindow.MoveAndResize(_workArea);
        BringToForeground("visible");
        _root.Focus(FocusState.Programmatic);
        BeginOpenAnimation();
        ShowInfoPanel();
        await FadeInNativeWindowAsync();
    }

    private Grid BuildImageViewport()
    {
        var viewport = new Grid
        {
            Children = { _previewImage },
        };
        viewport.RightTapped += ImageViewport_RightTapped;
        AutomationProperties.SetName(viewport, AppStrings.Get("Preview favorite image"));
        return viewport;
    }

    private Grid BuildSurface()
    {
        return new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0,
            RenderTransform = _surfaceScale,
            RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
            Children = { _imageViewport },
        };
    }

    private static TextBlock BuildInfoText()
    {
        return new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.NoWrap,
            Foreground = new SolidColorBrush(WinUIColor.FromArgb(255, 245, 245, 245)),
        };
    }

    private static Border BuildInfoPanel(TextBlock text)
    {
        return new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(24),
            Padding = new Thickness(14, 8, 14, 8),
            CornerRadius = new CornerRadius(18),
            Background = new SolidColorBrush(WinUIColor.FromArgb(178, 20, 20, 24)),
            Opacity = 0,
            Child = text,
        };
    }

    private async Task ShowCurrentItemAsync()
    {
        if (_items.Count == 0)
        {
            await ClosePreviewAsync();
            return;
        }

        _currentIndex = NormalizeIndex(_currentIndex);
        var item = CurrentItem;
        var bitmap = new BitmapImage
        {
            DecodePixelWidth = PreviewDecodePixelWidth,
        };

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(item.ImagePath);
            using IRandomAccessStream stream = await file.OpenReadAsync();
            await bitmap.SetSourceAsync(stream);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Favorite preview image preload failed: {ex.Message}");
            bitmap.UriSource = new Uri(item.ImagePath, UriKind.Absolute);
        }

        _previewBitmap = bitmap;
        _previewImage.Source = bitmap;
        UpdateSurfaceSizeForBitmap(bitmap);
        ResetZoomAndPan();
        UpdateInfoText();
        ShowInfoPanel();
    }

    private apod_wallpaper.FavoriteApodItem CurrentItem => _items[_currentIndex];

    private void ConfigureWindow()
    {
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

    private void Root_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_root);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        _singleClickCloseCts?.Cancel();
        _pointerPressed = true;
        _pointerStartedOnImage = IsWithin(_imageViewport, e.OriginalSource as DependencyObject);
        _isDragging = false;
        _pressPoint = point.Position;
        _startPanX = _panX;
        _startPanY = _panY;
        _root.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void Root_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        ShowInfoPanel();

        if (!_pointerPressed)
            return;

        var point = e.GetCurrentPoint(_root);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        var deltaX = point.Position.X - _pressPoint.X;
        var deltaY = point.Position.Y - _pressPoint.Y;
        var distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distance >= DragThreshold)
            _isDragging = true;

        if (_isDragging && _pointerStartedOnImage && _zoom > 1)
        {
            _panX = _startPanX + deltaX;
            _panY = _startPanY + deltaY;
            ClampPan();
            ApplyImageTransform();
            ShowInfoPanel();
        }

        e.Handled = true;
    }

    private void InfoPanel_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _singleClickCloseCts?.Cancel();
        ShowInfoPanel();
        e.Handled = true;
    }

    private void InfoPanel_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        ShowInfoPanel();
        e.Handled = true;
    }

    private async void Root_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_pointerPressed)
            return;

        var wasDragging = _isDragging;
        _pointerPressed = false;
        _pointerStartedOnImage = false;
        _isDragging = false;
        _root.ReleasePointerCapture(e.Pointer);
        e.Handled = true;

        if (wasDragging)
        {
            _lastClickUtc = DateTime.MinValue;
            return;
        }

        await HandleSimpleClickAsync(e.GetCurrentPoint(_root).Position);
    }

    private void Root_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        ResetPointerGesture();
    }

    private void Root_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        ResetPointerGesture();
    }

    private void ResetPointerGesture()
    {
        _pointerPressed = false;
        _pointerStartedOnImage = false;
        _isDragging = false;
    }

    private async Task HandleSimpleClickAsync(Windows.Foundation.Point point)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastClickUtc).TotalMilliseconds <= SingleClickCloseDelayMs &&
            Distance(point, _lastClickPoint) <= DragThreshold)
        {
            _singleClickCloseCts?.Cancel();
            _lastClickUtc = DateTime.MinValue;
            ResetZoomAndPan();
            ShowInfoPanel();
            return;
        }

        _lastClickUtc = now;
        _lastClickPoint = point;
        var cts = new CancellationTokenSource();
        _singleClickCloseCts = cts;
        try
        {
            await Task.Delay(SingleClickCloseDelayMs, cts.Token);
            if (!cts.IsCancellationRequested)
                await ClosePreviewAsync();
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void Root_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!IsWithin(_imageViewport, e.OriginalSource as DependencyObject))
            return;

        var delta = e.GetCurrentPoint(_root).Properties.MouseWheelDelta;
        if (delta == 0)
            return;

        var zoomDelta = delta > 0 ? WheelZoomStep : -WheelZoomStep;
        SetZoom(_zoom + zoomDelta);
        ShowInfoPanel();
        e.Handled = true;
    }

    private void ImageViewport_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (_items.Count == 0)
            return;

        e.Handled = true;
        ShowInfoPanel();
        var menu = FavoriteContextMenuFactory.Create(CurrentItem, new FavoriteImageActions(
            _actions.SetAsWallpaperAsync,
            OpenCurrentItemInCalendar,
            _actions.OpenInFolder,
            RemoveCurrentItemFromFavoritesAsync));
        menu.ShowAt(_root, e.GetPosition(_root));
    }

    private async void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape || e.Key == VirtualKey.Space)
        {
            e.Handled = true;
            await ClosePreviewAsync();
            return;
        }

        if (e.Key == VirtualKey.Left || e.Key == VirtualKey.A)
        {
            e.Handled = true;
            await ShowPreviousAsync();
            return;
        }

        if (e.Key == VirtualKey.Right || e.Key == VirtualKey.D)
        {
            e.Handled = true;
            await ShowNextAsync();
            return;
        }

        if (e.Key == VirtualKey.Number0 && IsControlKeyDown())
        {
            e.Handled = true;
            ResetZoomAndPan();
            ShowInfoPanel();
        }
    }

    private static bool IsControlKeyDown()
    {
        var state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
        return (state & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    }

    private async Task ShowPreviousAsync()
    {
        if (_items.Count == 0)
            return;

        _currentIndex = NormalizeIndex(_currentIndex - 1);
        await ShowCurrentItemAsync();
    }

    private async Task ShowNextAsync()
    {
        if (_items.Count == 0)
            return;

        _currentIndex = NormalizeIndex(_currentIndex + 1);
        await ShowCurrentItemAsync();
    }

    private void OpenCurrentItemInCalendar(apod_wallpaper.FavoriteApodItem item)
    {
        _ = OpenCurrentItemInCalendarAsync(item);
    }

    private async Task OpenCurrentItemInCalendarAsync(apod_wallpaper.FavoriteApodItem item)
    {
        await ClosePreviewAsync();
        _actions.OpenInCalendar(item);
    }

    private async Task<bool> RemoveCurrentItemFromFavoritesAsync(apod_wallpaper.FavoriteApodItem item)
    {
        var removed = await _actions.RemoveFromFavoritesAsync(item);
        if (!removed)
            return false;

        var removedIndex = _items.FindIndex(candidate => candidate.Date.Date == item.Date.Date);
        if (removedIndex >= 0)
            _items.RemoveAt(removedIndex);

        if (_items.Count == 0)
        {
            await ClosePreviewAsync();
            return true;
        }

        _currentIndex = Math.Min(Math.Max(0, removedIndex), _items.Count - 1);
        await ShowCurrentItemAsync();
        return true;
    }

    private void SetZoom(double value)
    {
        _zoom = Math.Clamp(value, MinZoom, MaxZoom);
        if (_zoom <= 1)
        {
            _panX = 0;
            _panY = 0;
        }

        ClampPan();
        ApplyImageTransform();
        UpdateInfoText();
    }

    private void ResetZoomAndPan()
    {
        _zoom = 1;
        _panX = 0;
        _panY = 0;
        ApplyImageTransform();
        UpdateInfoText();
    }

    private void UpdateSurfaceSizeForBitmap(BitmapImage bitmap)
    {
        var viewportWidth = ResolveViewportWidth();
        var viewportHeight = ResolveViewportHeight();
        var bitmapWidth = bitmap.PixelWidth > 0 ? bitmap.PixelWidth : viewportWidth;
        var bitmapHeight = bitmap.PixelHeight > 0 ? bitmap.PixelHeight : viewportHeight;
        var fitScale = Math.Min(
            (viewportWidth * SurfaceScreenRatio) / bitmapWidth,
            (viewportHeight * SurfaceScreenRatio) / bitmapHeight);

        _surface.Width = Math.Max(1, bitmapWidth * fitScale);
        _surface.Height = Math.Max(1, bitmapHeight * fitScale);
    }

    private double ResolveViewportWidth()
    {
        return Math.Max(1, _root.ActualWidth > 0 ? _root.ActualWidth : _workArea.Width);
    }

    private double ResolveViewportHeight()
    {
        return Math.Max(1, _root.ActualHeight > 0 ? _root.ActualHeight : _workArea.Height);
    }

    private void ClampPan()
    {
        var bounds = ResolvePanBounds();
        _panX = Math.Clamp(_panX, -bounds.X, bounds.X);
        _panY = Math.Clamp(_panY, -bounds.Y, bounds.Y);
    }

    private Windows.Foundation.Point ResolvePanBounds()
    {
        var viewportWidth = ResolveViewportWidth();
        var viewportHeight = ResolveViewportHeight();
        var surfaceWidth = Math.Max(1, _surface.ActualWidth > 0 ? _surface.ActualWidth : _surface.Width);
        var surfaceHeight = Math.Max(1, _surface.ActualHeight > 0 ? _surface.ActualHeight : _surface.Height);
        var renderedWidth = surfaceWidth * _zoom;
        var renderedHeight = surfaceHeight * _zoom;
        return new Windows.Foundation.Point(
            Math.Max(0, (renderedWidth - viewportWidth) / 2),
            Math.Max(0, (renderedHeight - viewportHeight) / 2));
    }

    private void Root_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_previewBitmap == null)
            return;

        UpdateSurfaceSizeForBitmap(_previewBitmap);
        ClampPan();
        ApplyImageTransform();
    }

    private void ApplyImageTransform()
    {
        _imageTransform.ScaleX = _zoom;
        _imageTransform.ScaleY = _zoom;
        _imageTransform.TranslateX = _panX;
        _imageTransform.TranslateY = _panY;
    }

    private void UpdateInfoText()
    {
        if (_items.Count == 0)
            return;

        _infoText.Text = string.Format(
            CultureInfo.InvariantCulture,
            "{0}   {1}%   {2}/{3}",
            CurrentItem.Date.ToString("dd MMM yyyy", AppStrings.DateCulture),
            Math.Round(_zoom * 100),
            _currentIndex + 1,
            _items.Count);
    }

    private void ShowInfoPanel()
    {
        _infoPanelTimer.Stop();
        AnimateOpacity(_infoPanel, 1, 100);
        _infoPanelTimer.Start();
    }

    private void InfoPanelTimer_Tick(object? sender, object e)
    {
        _infoPanelTimer.Stop();
        AnimateOpacity(_infoPanel, 0, 180);
    }

    private void FavoriteImagePreviewWindow_Closed(object sender, WindowEventArgs args)
    {
        _singleClickCloseCts?.Cancel();
        _infoPanelTimer.Stop();
        _infoPanelTimer.Tick -= InfoPanelTimer_Tick;
        _infoPanel.PointerPressed -= InfoPanel_PointerPressed;
        _infoPanel.PointerReleased -= InfoPanel_PointerReleased;
        _root.SizeChanged -= Root_SizeChanged;
        RemoveMouseActivateGuard();
    }

    private static double Distance(Windows.Foundation.Point first, Windows.Foundation.Point second)
    {
        var deltaX = first.X - second.X;
        var deltaY = first.Y - second.Y;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private int NormalizeIndex(int index)
    {
        if (_items.Count == 0)
            return 0;

        if (index < 0)
            return _items.Count - 1;

        if (index >= _items.Count)
            return 0;

        return index;
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

    private void BeginOpenAnimation()
    {
        if (_openAnimationStarted)
            return;

        _openAnimationStarted = true;
        AnimateOpen();
    }

    private void AnimateOpen()
    {
        var storyboard = new Storyboard();
        storyboard.Children.Add(CreateOpacityAnimation(_tintLayer, 1, BackgroundFadeInMs));
        storyboard.Children.Add(CreateOpacityAnimation(_surface, 1, SurfaceFadeInMs));
        storyboard.Children.Add(CreateScaleAnimation(_surfaceScale, "ScaleX", 1, SurfaceFadeInMs));
        storyboard.Children.Add(CreateScaleAnimation(_surfaceScale, "ScaleY", 1, SurfaceFadeInMs));
        storyboard.Begin();
    }

    private static void AnimateOpacity(UIElement target, double to, int durationMs)
    {
        var storyboard = new Storyboard();
        storyboard.Children.Add(CreateOpacityAnimation(target, to, durationMs));
        storyboard.Begin();
    }

    private static DoubleAnimation CreateOpacityAnimation(UIElement target, double to, int durationMs)
    {
        var animation = new DoubleAnimation
        {
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, "Opacity");
        return animation;
    }

    private static DoubleAnimation CreateScaleAnimation(ScaleTransform target, string property, double to, int durationMs)
    {
        var animation = new DoubleAnimation
        {
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        return animation;
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
        _singleClickCloseCts?.Cancel();
        await FadeOutNativeWindowAsync();
        Close();
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
