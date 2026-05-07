using System.Runtime.InteropServices.WindowsRuntime;
using CaptureImage.Core.Abstractions;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Storage.Streams;
using Windows.System;
using WinRT.Interop;

namespace CaptureImage.UI.Views;

/// <summary>
/// Full-screen primary-monitor overlay that lets the user drag a rectangle over the
/// pre-captured desktop screenshot. Returns the selected region in pixel coordinates
/// via <see cref="ResultAsync"/>. Esc cancels (yields null).
/// </summary>
/// <remarks>
/// v1.5 covers the primary monitor only. Multi-monitor + per-monitor DPI is on the
/// v1.6 roadmap. The overlay shows the captured screen dimmed at 35% opacity; the
/// user's selection rectangle re-shows the image through a clip so the chosen area
/// stands out without re-capturing (which would race with the overlay itself).
/// </remarks>
public sealed partial class RegionSelectorOverlay : Window
{
    private readonly TaskCompletionSource<RegionSelectionResult?> _result =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly int _screenWidth;
    private readonly int _screenHeight;
    private readonly byte[] _backgroundPngBytes;

    private bool _selecting;
    private Point _selectionStart;

    public RegionSelectorOverlay(byte[] backgroundPngBytes, int screenWidth, int screenHeight, string hintText)
    {
        InitializeComponent();

        _backgroundPngBytes = backgroundPngBytes;
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;
        HintText.Text = hintText;

        Title = "CaptureImage region";
        ExtendsContentIntoTitleBar = true;

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        // Borderless full-screen presenter so there's no chrome and no taskbar visible.
        appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        appWindow.MoveAndResize(new RectInt32(0, 0, screenWidth, screenHeight));

        Closed += OnClosed;
        Activated += OnActivated;

        var bitmap = new BitmapImage();
        using (var stream = new InMemoryRandomAccessStream())
        {
            stream.WriteAsync(backgroundPngBytes.AsBuffer()).AsTask().GetAwaiter().GetResult();
            stream.Seek(0);
            bitmap.SetSource(stream);
        }
        BackgroundImage.Source = bitmap;
        SelectionImage.Source = bitmap;
    }

    /// <summary>Resolves to the selected rectangle in pixel coords, or null if cancelled.</summary>
    public Task<RegionSelectionResult?> ResultAsync => _result.Task;

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        // Ensure the overlay grabs keyboard focus so Esc/Enter are routed here.
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {
            Root.Focus(FocusState.Programmatic);
        }
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _selecting = true;
        _selectionStart = e.GetCurrentPoint(Root).Position;
        SelectionBorder.Visibility = Visibility.Visible;
        DimensionBadge.Visibility = Visibility.Visible;
        UpdateSelection(_selectionStart);
        Root.CapturePointer(e.Pointer);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_selecting) return;
        UpdateSelection(e.GetCurrentPoint(Root).Position);
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_selecting) return;
        _selecting = false;
        Root.ReleasePointerCapture(e.Pointer);

        var end = e.GetCurrentPoint(Root).Position;
        var rect = NormalizeRect(_selectionStart, end);

        if (rect.Width < 4 || rect.Height < 4)
        {
            // Treat micro-drags as a cancel — likely a mis-click.
            _result.TrySetResult(null);
            Close();
            return;
        }

        // The overlay is sized to the screen pixel dimensions, so DIP coords map 1:1
        // when the overlay window's DPI matches the source capture (true on a single
        // 100% DPI monitor; v1.6 will add per-monitor scaling).
        _result.TrySetResult(new RegionSelectionResult(
            X: (int)Math.Round(rect.X),
            Y: (int)Math.Round(rect.Y),
            Width: (int)Math.Round(rect.Width),
            Height: (int)Math.Round(rect.Height)));
        Close();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            _result.TrySetResult(null);
            Close();
            e.Handled = true;
        }
    }

    private void UpdateSelection(Point current)
    {
        var rect = NormalizeRect(_selectionStart, current);
        Canvas.SetLeft(SelectionBorder, rect.X);
        Canvas.SetTop(SelectionBorder, rect.Y);
        SelectionBorder.Width = rect.Width;
        SelectionBorder.Height = rect.Height;

        // Position the inner image so it lines up with the same pixels of the underlying
        // screenshot — i.e. the visible selection shows the image at full opacity for
        // exactly the chosen region.
        SelectionImage.Width = _screenWidth;
        SelectionImage.Height = _screenHeight;
        Canvas.SetLeft(SelectionImage, -rect.X);
        Canvas.SetTop(SelectionImage, -rect.Y);

        DimensionText.Text = $"{(int)rect.Width} × {(int)rect.Height}";
        Canvas.SetLeft(DimensionBadge, rect.X);
        Canvas.SetTop(DimensionBadge, rect.Y - 26);
    }

    private static Rect NormalizeRect(Point a, Point b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        var w = Math.Abs(a.X - b.X);
        var h = Math.Abs(a.Y - b.Y);
        return new Rect(x, y, w, h);
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _result.TrySetResult(null);
    }
}

/// <summary>Pixel-coordinate result yielded by <see cref="RegionSelectorOverlay"/>.</summary>
public sealed record RegionSelectionResult(int X, int Y, int Width, int Height);
