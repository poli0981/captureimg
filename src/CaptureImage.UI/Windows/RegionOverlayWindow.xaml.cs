// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.UI.Imaging;
using CaptureImage.UI.Interop;

namespace CaptureImage.UI.Windows;

/// <summary>
/// Covers one monitor with the frozen picture taken when the hotkey was pressed; the user
/// drags a rectangle (a plain click takes the whole monitor). The window is placed in physical
/// pixels and the picture is shown at the monitor's DPI, so one image pixel is one screen pixel.
/// </summary>
public partial class RegionOverlayWindow : Window
{
    private const int MinimumSize = 4;
    private readonly MonitorSnapshot _snapshot;
    private Point? _start;
    private Rect? _selection;

    public RegionOverlayWindow(MonitorSnapshot snapshot)
    {
        _snapshot = snapshot;
        InitializeComponent();
        Frozen.Source = FrameBitmap.ToBitmapSource(snapshot.DisplayFrame, snapshot.Monitor.Dpi);

        var bounds = snapshot.Monitor.Bounds;
        var scale = snapshot.Monitor.Scale;
        Left = bounds.X / scale;
        Top = bounds.Y / scale;
        Width = bounds.Width / scale;
        Height = bounds.Height / scale;

        SourceInitialized += (_, _) =>
        {
            WindowInterop.ExcludeFromCapture(this);
            WindowInterop.SetPhysicalBounds(this, bounds);
        };
        SizeChanged += (_, _) => UpdateVisuals();
    }

    /// <summary>The user finished a selection (relative to the monitor, physical pixels).</summary>
    public event EventHandler<RegionSelection>? Completed;

    public event EventHandler? Cancelled;

    /// <summary>A drag started here; the other overlays clear theirs.</summary>
    public event EventHandler? SelectionStarted;

    public bool HasSelection => _selection is { Width: > 0, Height: > 0 };

    public void ClearSelection()
    {
        _start = null;
        _selection = null;
        UpdateVisuals();
    }

    /// <summary>The dragged rectangle, or the whole monitor when nothing is selected.</summary>
    public RegionSelection CurrentOrFullSelection()
    {
        var area = _selection is { } rect ? ToPixels(rect) : default;
        return area.Width >= MinimumSize && area.Height >= MinimumSize
            ? new RegionSelection(_snapshot.Monitor, area)
            : FullMonitor();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        // WPF moves the window to the "suggested" rectangle; put it back over the monitor.
        Dispatcher.BeginInvoke(() => WindowInterop.SetPhysicalBounds(this, _snapshot.Monitor.Bounds));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _start = e.GetPosition(Root);
        _selection = new Rect(_start.Value, _start.Value);
        Hint.Visibility = Visibility.Collapsed;
        CaptureMouse();
        SelectionStarted?.Invoke(this, EventArgs.Empty);
        UpdateVisuals();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_start is { } start && IsMouseCaptured)
        {
            _selection = new Rect(start, Clamp(e.GetPosition(Root)));
            UpdateVisuals();
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!IsMouseCaptured)
        {
            return;
        }

        ReleaseMouseCapture();
        Completed?.Invoke(this, CurrentOrFullSelection());
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            Completed?.Invoke(this, CurrentOrFullSelection());
            e.Handled = true;
        }
    }

    private RegionSelection FullMonitor() =>
        new(_snapshot.Monitor, new PixelRect(0, 0, _snapshot.Monitor.Bounds.Width, _snapshot.Monitor.Bounds.Height));

    private Point Clamp(Point point) =>
        new(Math.Clamp(point.X, 0, Root.ActualWidth), Math.Clamp(point.Y, 0, Root.ActualHeight));

    /// <summary>DIP rectangle → physical pixels relative to the monitor, clamped to it.</summary>
    private PixelRect ToPixels(Rect rect)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var bounds = _snapshot.Monitor.Bounds;
        var x = (int)Math.Round(rect.X * dpi.DpiScaleX);
        var y = (int)Math.Round(rect.Y * dpi.DpiScaleY);
        var right = (int)Math.Round(rect.Right * dpi.DpiScaleX);
        var bottom = (int)Math.Round(rect.Bottom * dpi.DpiScaleY);
        return new PixelRect(x, y, right - x, bottom - y).Intersect(new PixelRect(0, 0, bounds.Width, bounds.Height));
    }

    private void UpdateVisuals()
    {
        var full = new Rect(0, 0, Root.ActualWidth, Root.ActualHeight);
        if (_selection is not { } rect || rect.Width <= 0 || rect.Height <= 0)
        {
            Shade.Data = new RectangleGeometry(full);
            SelectionBorder.Visibility = Visibility.Collapsed;
            SizeLabel.Visibility = Visibility.Collapsed;
            return;
        }

        Shade.Data = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(full), new RectangleGeometry(rect));
        SelectionBorder.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionBorder, rect.X);
        Canvas.SetTop(SelectionBorder, rect.Y);
        SelectionBorder.Width = rect.Width;
        SelectionBorder.Height = rect.Height;

        var pixels = ToPixels(rect);
        SizeText.Text = string.Create(CultureInfo.InvariantCulture, $"{pixels.Width} × {pixels.Height}");
        SizeLabel.Visibility = Visibility.Visible;
        SizeLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var labelTop = rect.Y - SizeLabel.DesiredSize.Height - 6;
        Canvas.SetLeft(SizeLabel, rect.X);
        Canvas.SetTop(SizeLabel, labelTop >= 0 ? labelTop : rect.Bottom + 6);
    }
}
