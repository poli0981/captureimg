// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CaptureImage.UI.Imaging;
using CaptureImage.UI.Windows;
using CaptureImage.ViewModels.Preview;
using CaptureImage.ViewModels.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace CaptureImage.UI.Services;

/// <summary>
/// Notifications: a snackbar inside the main window while it has focus, otherwise a small
/// non-activating pop-up above the taskbar (never steals focus from a game, never captured).
/// </summary>
public sealed class ToastService(Dispatcher dispatcher, ShellContext shell, ISnackbarService snackbar) : IToastService
{
    private const int MaxPopups = 3;
    private readonly List<ToastWindow> _popups = [];

    public void Show(ToastKind kind, string title, string message, TimeSpan? duration = null) => dispatcher.BeginInvoke(() =>
    {
        var timeout = duration ?? TimeSpan.FromSeconds(kind == ToastKind.Error ? 6 : 3.5);
        if (shell.IsMainWindowActive && snackbar.GetSnackbarPresenter() is not null)
        {
            snackbar.Show(title, message, Appearance(kind), new SymbolIcon(Symbol(kind)), timeout);
            return;
        }

        while (_popups.Count >= MaxPopups)
        {
            _popups[0].Close();
        }

        var popup = new ToastWindow(kind, title, message, Symbol(kind), timeout);
        popup.Closed += (_, _) =>
        {
            _popups.Remove(popup);
            Arrange();
        };
        _popups.Add(popup);
        popup.Show();
        Arrange();
    });

    /// <summary>Stack the pop-ups upwards from the bottom-right corner of the primary work area.</summary>
    private void Arrange()
    {
        var area = SystemParameters.WorkArea;
        var bottom = area.Bottom;
        for (var i = _popups.Count - 1; i >= 0; i--)
        {
            var popup = _popups[i];
            popup.UpdateLayout();
            var height = popup.ActualHeight > 0 ? popup.ActualHeight : 96;
            popup.Left = area.Right - popup.Width;
            popup.Top = bottom - height;
            bottom -= height;
        }
    }

    private static ControlAppearance Appearance(ToastKind kind) => kind switch
    {
        ToastKind.Success => ControlAppearance.Success,
        ToastKind.Warning => ControlAppearance.Caution,
        ToastKind.Error => ControlAppearance.Danger,
        _ => ControlAppearance.Secondary,
    };

    private static SymbolRegular Symbol(ToastKind kind) => kind switch
    {
        ToastKind.Success => SymbolRegular.CheckmarkCircle24,
        ToastKind.Warning => SymbolRegular.Warning24,
        ToastKind.Error => SymbolRegular.ErrorCircle24,
        _ => SymbolRegular.Info24,
    };
}

/// <summary>Clipboard access on the UI thread; images also carry a PNG copy for apps that read it.</summary>
public sealed partial class ClipboardService(Dispatcher dispatcher, ILogger<ClipboardService> logger) : IClipboardService
{
    public async Task<bool> CopyImageAsync(CapturedFrame frame, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var (bitmap, png) = await Task.Run(() =>
        {
            var source = FrameBitmap.ToBitmapSource(frame);
            return (source, FrameBitmap.EncodePng(source));
        }, cancellationToken).ConfigureAwait(false);

        return await dispatcher.InvokeAsync(() => TrySet(() =>
        {
            var data = new DataObject();
            data.SetImage(bitmap);
            data.SetData("PNG", png, autoConvert: false);
            Clipboard.SetDataObject(data, copy: true);
        })).Task.ConfigureAwait(false);
    }

    public Task<bool> CopyTextAsync(string text, CancellationToken cancellationToken = default) =>
        dispatcher.InvokeAsync(() => TrySet(() => Clipboard.SetDataObject(text ?? string.Empty, copy: true))).Task;

    private bool TrySet(Action set)
    {
        try
        {
            set();
            return true;
        }
        catch (ExternalException ex)
        {
            // Another program holds the clipboard open (WPF already retried).
            LogClipboardBusy(ex);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The clipboard is in use by another program.")]
    private partial void LogClipboardBusy(Exception exception);
}

/// <summary>Common file dialogs and confirmations (a content dialog inside the main window when it is visible).</summary>
public sealed class DialogService(ShellContext shell, IContentDialogService contentDialogs) : IDialogService
{
    public Task<string?> PickFolderAsync(string title, string? initialFolder = null)
    {
        var dialog = new OpenFolderDialog { Title = title, Multiselect = false };
        if (!string.IsNullOrEmpty(initialFolder) && Directory.Exists(initialFolder))
        {
            dialog.InitialDirectory = initialFolder;
        }
        return Task.FromResult(Show(dialog) ? dialog.FolderName : null);
    }

    public Task<string?> PickFileAsync(string title, string filter)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true, Multiselect = false };
        return Task.FromResult(Show(dialog) ? dialog.FileName : null);
    }

    public Task<string?> SaveFileAsync(string title, string defaultFileName, string filter)
    {
        var dialog = new SaveFileDialog { Title = title, Filter = filter, FileName = defaultFileName, OverwritePrompt = true, AddExtension = true };
        return Task.FromResult(Show(dialog) ? dialog.FileName : null);
    }

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText)
    {
        if (shell.IsMainWindowVisible && contentDialogs.GetDialogHostEx() is not null)
        {
            var result = await contentDialogs.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions
                {
                    Title = title,
                    Content = message,
                    PrimaryButtonText = confirmText,
                    CloseButtonText = cancelText,
                },
                CancellationToken.None).ConfigureAwait(true);
            return result == ContentDialogResult.Primary;
        }

        return System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.OK;
    }

    private bool Show(CommonDialog dialog) =>
        (shell.IsMainWindowVisible && shell.MainWindow is { } owner ? dialog.ShowDialog(owner) : dialog.ShowDialog()) == true;
}

/// <summary>Shows the "Save this capture?" window and waits for the answer.</summary>
public sealed class PreviewPresenter(
    Dispatcher dispatcher,
    IOcrService ocr,
    IClipboardService clipboard,
    IToastService toasts,
    ILocalizationService localization) : IPreviewPresenter
{
    public Task<bool> ShowAsync(CapturedFrame frame, string title, CancellationToken cancellationToken = default) =>
        dispatcher.InvokeAsync(async () =>
        {
            var viewModel = new PreviewViewModel(frame, title, ocr, clipboard, toasts, localization);
            var window = new PreviewWindow(viewModel);
            await using var registration = cancellationToken.Register(() => dispatcher.BeginInvoke(() => viewModel.DiscardCommand.Execute(null)));
            window.Show();
            window.Activate();
            try
            {
                return await viewModel.Result.ConfigureAwait(true);
            }
            finally
            {
                window.CloseQuietly();
            }
        }).Task.Unwrap();
}

/// <summary>Region selection: one frozen full-screen overlay per monitor.</summary>
public sealed class RegionSelector(Dispatcher dispatcher, IHotkeyService hotkeys) : IRegionSelector
{
    private static readonly HotkeyBinding EscapeKey = new(HotkeyModifiers.None, 0x1B);
    private static readonly HotkeyBinding EnterKey = new(HotkeyModifiers.None, 0x0D);

    public Task<RegionSelection?> SelectAsync(IReadOnlyList<MonitorSnapshot> snapshots, CancellationToken cancellationToken = default) =>
        dispatcher.InvokeAsync(() => RunAsync(snapshots, cancellationToken)).Task.Unwrap();

    private async Task<RegionSelection?> RunAsync(IReadOnlyList<MonitorSnapshot> snapshots, CancellationToken cancellationToken)
    {
        if (snapshots.Count == 0)
        {
            return null;
        }

        var result = new TaskCompletionSource<RegionSelection?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var overlays = snapshots.Select(s => new RegionOverlayWindow(s)).ToList();
        foreach (var overlay in overlays)
        {
            overlay.Completed += (_, selection) => result.TrySetResult(selection);
            overlay.Cancelled += (_, _) => result.TrySetResult(null);
            overlay.SelectionStarted += (sender, _) =>
            {
                foreach (var other in overlays.Where(o => !ReferenceEquals(o, sender)))
                {
                    other.ClearSelection();
                }
            };
        }

        // Esc / Enter work even when a full-screen game keeps the keyboard focus.
        using var keys = hotkeys.RegisterTemporary([EscapeKey, EnterKey], key => dispatcher.BeginInvoke(() =>
        {
            if (key == EscapeKey)
            {
                result.TrySetResult(null);
                return;
            }
            var active = overlays.FirstOrDefault(o => o.HasSelection) ?? overlays.FirstOrDefault(o => o.IsMouseOver) ?? overlays[0];
            result.TrySetResult(active.CurrentOrFullSelection());
        }));
        await using var cancellation = cancellationToken.Register(() => result.TrySetResult(null));

        foreach (var overlay in overlays)
        {
            overlay.Show();
        }
        (overlays.FirstOrDefault(o => o.IsMouseOver) ?? overlays[0]).Activate();

        try
        {
            return await result.Task.ConfigureAwait(true);
        }
        finally
        {
            foreach (var overlay in overlays)
            {
                overlay.Close();
            }
        }
    }
}

/// <summary>Always-on-top thumbnails of recent captures (at most six; the oldest closes first).</summary>
public sealed class PinnedThumbnailHost(Dispatcher dispatcher, IShellService shell, IClipboardService clipboard, IImageDecoder decoder) : IPinnedThumbnailHost
{
    private const int MaxPinned = 6;
    private const int MaxDimension = 480;
    private readonly List<PinnedThumbnailWindow> _windows = [];

    public void Show(CapturedFrame frame, string? filePath, string title)
    {
        ArgumentNullException.ThrowIfNull(frame);
        _ = ShowAsync(frame, filePath, title);
    }

    private async Task ShowAsync(CapturedFrame frame, string? filePath, string title)
    {
        var bitmap = await Task.Run(() => FrameBitmap.Downscale(FrameBitmap.ToBitmapSource(frame), MaxDimension)).ConfigureAwait(false);
        await dispatcher.InvokeAsync(() =>
        {
            while (_windows.Count >= MaxPinned)
            {
                _windows[0].Close();
            }

            var window = new PinnedThumbnailWindow(bitmap, filePath, title, shell, clipboard, decoder);
            window.Closed += (_, _) => _windows.Remove(window);
            var area = SystemParameters.WorkArea;
            window.Left = area.Right - window.Width - 16 - (_windows.Count % 3 * 24);
            window.Top = area.Top + 16 + (_windows.Count * 24);
            _windows.Add(window);
            window.Show();
        });
    }
}
