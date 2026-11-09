// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;
using CaptureImage.UI.Accessibility;
using CaptureImage.UI.Controls;
using CaptureImage.UI.Imaging;
using CaptureImage.UI.Interop;

namespace CaptureImage.UI.Windows;

/// <summary>
/// Covers one monitor with the frozen picture taken when the hotkey was pressed; the user
/// drags a rectangle (a plain click takes the whole monitor) or uses the keys of
/// <see cref="RegionKeyboard"/>. The window is placed in physical pixels and the picture is
/// shown at the monitor's DPI, so one image pixel is one screen pixel. The selection is kept
/// in physical pixels relative to the monitor.
/// </summary>
public partial class RegionOverlayWindow : Window
{
    private readonly MonitorSnapshot _snapshot;
    private (int X, int Y)? _start;
    private PixelRect? _selection;

    public RegionOverlayWindow(MonitorSnapshot snapshot)
    {
        _snapshot = snapshot;
        InitializeComponent();
        Frozen.Source = FrameBitmap.ToBitmapSource(snapshot.DisplayFrame, snapshot.Monitor.Dpi);
        if (SystemParameters.HighContrast)
        {
            UseContrastColors();
        }

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

    /// <summary>A selection started here; the other overlays clear theirs.</summary>
    public event EventHandler? SelectionStarted;

    /// <summary>
    /// A key of <see cref="RegionKeyboard"/> reached this window (it wasn't registered as a
    /// temporary hotkey); the selector handles it like the registered ones.
    /// </summary>
    public event EventHandler<HotkeyBinding>? KeyPressed;

    /// <summary>The monitor this overlay covers, in virtual-desktop pixels.</summary>
    public PixelRect MonitorBounds => _snapshot.Monitor.Bounds;

    /// <summary>A normal keyboard step on this monitor, in physical pixels.</summary>
    public int KeyboardStep => RegionKeyboard.StepFor(_snapshot.Monitor.Scale);

    public bool HasSelection => _selection is { IsEmpty: false };

    public void ClearSelection()
    {
        _start = null;
        _selection = null;
        UpdateVisuals();
    }

    /// <summary>The selected rectangle, or the whole monitor when nothing (or too little) is selected.</summary>
    public RegionSelection CurrentOrFullSelection() =>
        _selection is { Width: >= RegionKeyboard.MinimumSize, Height: >= RegionKeyboard.MinimumSize } area
            ? new RegionSelection(_snapshot.Monitor, area)
            : FullMonitor();

    /// <summary>Move or resize the selection from the keyboard (creating one first if there is none).</summary>
    public void ApplyKeyboard(RegionKeyCommand command)
    {
        var bounds = _snapshot.Monitor.Bounds;
        var next = RegionKeyboard.Apply(_selection, command, bounds.Width, bounds.Height);
        if (next == _selection)
        {
            return;
        }

        if (!HasSelection)
        {
            SelectionStarted?.Invoke(this, EventArgs.Empty);
        }

        _start = null;
        _selection = next;
        Hint.Visibility = Visibility.Collapsed;
        UpdateVisuals();
        LiveRegion.Announce(SizeText);
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
        var start = ToPixels(e.GetPosition(Root));
        _start = start;
        _selection = new PixelRect(start.X, start.Y, 0, 0);
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
            var (x, y) = ToPixels(e.GetPosition(Root));
            _selection = new PixelRect(Math.Min(start.X, x), Math.Min(start.Y, y), Math.Abs(x - start.X), Math.Abs(y - start.Y));
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
        var key = KeyInput.Binding(e);
        if (RegionKeyboard.Map(key, KeyboardStep) is not null)
        {
            KeyPressed?.Invoke(this, key);
            e.Handled = true;
        }
    }

    private RegionSelection FullMonitor() =>
        new(_snapshot.Monitor, new PixelRect(0, 0, _snapshot.Monitor.Bounds.Width, _snapshot.Monitor.Bounds.Height));

    /// <summary>DIP position → physical pixel relative to the monitor, clamped to it.</summary>
    private (int X, int Y) ToPixels(Point point)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var bounds = _snapshot.Monitor.Bounds;
        return (
            Math.Clamp((int)Math.Round(point.X * dpi.DpiScaleX), 0, bounds.Width),
            Math.Clamp((int)Math.Round(point.Y * dpi.DpiScaleY), 0, bounds.Height));
    }

    /// <summary>Physical pixels relative to the monitor → DIP rectangle for drawing.</summary>
    private Rect ToDips(PixelRect rect)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        return new Rect(rect.X / dpi.DpiScaleX, rect.Y / dpi.DpiScaleY, rect.Width / dpi.DpiScaleX, rect.Height / dpi.DpiScaleY);
    }

    /// <summary>Windows contrast theme: draw the selection and labels with the theme's colours.</summary>
    private void UseContrastColors()
    {
        SelectionBorder.Stroke = SystemColors.HighlightBrush;
        SelectionBorder.StrokeThickness = 2;
        foreach (var label in new[] { SizeLabel, Hint })
        {
            label.Background = SystemColors.WindowBrush;
            label.BorderBrush = SystemColors.WindowTextBrush;
            label.BorderThickness = new Thickness(1);
        }
        SizeText.Foreground = SystemColors.WindowTextBrush;
        HintText.Foreground = SystemColors.WindowTextBrush;
    }

    private void UpdateVisuals()
    {
        var full = new Rect(0, 0, Root.ActualWidth, Root.ActualHeight);
        if (_selection is not { IsEmpty: false } selection)
        {
            Shade.Data = new RectangleGeometry(full);
            SelectionBorder.Visibility = Visibility.Collapsed;
            SizeLabel.Visibility = Visibility.Collapsed;
            return;
        }

        var rect = ToDips(selection);
        Shade.Data = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(full), new RectangleGeometry(rect));
        SelectionBorder.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionBorder, rect.X);
        Canvas.SetTop(SelectionBorder, rect.Y);
        SelectionBorder.Width = rect.Width;
        SelectionBorder.Height = rect.Height;

        SizeText.Text = string.Create(CultureInfo.InvariantCulture, $"{selection.Width} × {selection.Height}");
        SizeLabel.Visibility = Visibility.Visible;
        SizeLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var labelTop = rect.Y - SizeLabel.DesiredSize.Height - 6;
        Canvas.SetLeft(SizeLabel, rect.X);
        Canvas.SetTop(SizeLabel, labelTop >= 0 ? labelTop : rect.Bottom + 6);
    }
}
