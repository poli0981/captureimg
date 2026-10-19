// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CaptureImage.Core.Abstractions;
using CaptureImage.UI.Interop;
using CaptureImage.UI.Localization;

namespace CaptureImage.UI.Windows;

/// <summary>
/// A floating, always-on-top thumbnail of a capture. Drag to move, double-click to open the
/// file; it never takes focus and never appears in later captures.
/// </summary>
public partial class PinnedThumbnailWindow : Window
{
    private readonly string? _filePath;
    private readonly IShellService _shell;
    private readonly IClipboardService _clipboard;
    private readonly IImageDecoder _decoder;

    public PinnedThumbnailWindow(
        BitmapSource bitmap,
        string? filePath,
        string title,
        IShellService shell,
        IClipboardService clipboard,
        IImageDecoder decoder)
    {
        _filePath = filePath;
        _shell = shell;
        _clipboard = clipboard;
        _decoder = decoder;
        InitializeComponent();
        LocalizationSource.Attach(this);
        Thumbnail.Source = bitmap;
        AutomationProperties.SetName(this, title);
        ToolTip = title;

        var hasFile = !string.IsNullOrEmpty(filePath) && File.Exists(filePath);
        RevealButton.Visibility = hasFile ? Visibility.Visible : Visibility.Collapsed;
        CopyButton.Visibility = hasFile ? Visibility.Visible : Visibility.Collapsed;

        SourceInitialized += (_, _) =>
        {
            WindowInterop.MakeNonActivating(this);
            WindowInterop.ExcludeFromCapture(this);
        };
        Root.MouseEnter += (_, _) => Actions.Visibility = Visibility.Visible;
        Root.MouseLeave += (_, _) => Actions.Visibility = Visibility.Collapsed;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.ClickCount == 2 && _filePath is not null && File.Exists(_filePath))
        {
            _shell.Open(_filePath);
        }
        else if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnRevealClick(object sender, RoutedEventArgs e)
    {
        if (_filePath is not null)
        {
            _shell.RevealInExplorer(_filePath);
        }
    }

    private async void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (_filePath is null)
        {
            return;
        }

        var frame = await _decoder.LoadAsync(_filePath).ConfigureAwait(true);
        if (frame is not null)
        {
            await _clipboard.CopyImageAsync(frame).ConfigureAwait(true);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
