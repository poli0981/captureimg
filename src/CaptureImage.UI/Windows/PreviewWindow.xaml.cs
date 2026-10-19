// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.ComponentModel;
using CaptureImage.UI.Imaging;
using CaptureImage.UI.Interop;
using CaptureImage.UI.Localization;
using CaptureImage.ViewModels.Preview;
using Wpf.Ui.Controls;

namespace CaptureImage.UI.Windows;

/// <summary>"Save this capture?" — Enter saves, Esc (or closing the window) discards.</summary>
public partial class PreviewWindow : FluentWindow
{
    private readonly PreviewViewModel _viewModel;
    private bool _closingFromPresenter;
    private bool _closed;

    public PreviewWindow(PreviewViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        LocalizationSource.Attach(this);
        PreviewImage.Source = FrameBitmap.ToBitmapSource(viewModel.Frame);
        SourceInitialized += (_, _) => WindowInterop.ExcludeFromCapture(this);
        Closed += (_, _) => _closed = true;
    }

    /// <summary>Close after the user answered (no second "discard").</summary>
    public void CloseQuietly()
    {
        if (_closed)
        {
            return;
        }
        _closingFromPresenter = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!_closingFromPresenter)
        {
            _viewModel.DiscardCommand.Execute(null);
        }
    }
}
