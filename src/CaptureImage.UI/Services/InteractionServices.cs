// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;
using CaptureImage.UI.Accessibility;
using CaptureImage.UI.Imaging;
using CaptureImage.UI.Interop;
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
/// Both are read to screen readers and stay up at least as long as Windows' "Dismiss
/// notifications after" setting asks.
/// </summary>
public sealed class ToastService(Dispatcher dispatcher, ShellContext shell, ISnackbarService snackbar) : IToastService
{
    private const int MaxPopups = 3;
    private readonly List<ToastWindow> _popups = [];

    public void Show(ToastKind kind, string title, string message, TimeSpan? duration = null) => dispatcher.BeginInvoke(() =>
    {
        var timeout = duration ?? DefaultTimeout(kind);
        if (shell.IsMainWindowActive && snackbar.GetSnackbarPresenter() is not null)
        {
            snackbar.Show(title, message, Appearance(kind), new SymbolIcon(Symbol(kind)), timeout);
            if (shell.MainWindow is { } window)
            {
                Announcer.Announce(window, Announcer.Compose(title, message), important: kind == ToastKind.Error);
            }
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

    private static TimeSpan DefaultTimeout(ToastKind kind)
    {
        var own = TimeSpan.FromSeconds(kind == ToastKind.Error ? 6 : 3.5);
        return WindowInterop.NotificationDuration() is { } windows && windows > own ? windows : own;
    }

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

/// <summary>
/// Region selection: one frozen full-screen overlay per monitor, driven by the mouse or by the
/// keys of <see cref="RegionKeyboard"/>. The keys are registered as temporary hotkeys while the
/// overlays are up, so they work even when a full-screen game keeps the keyboard focus.
/// </summary>
public sealed class RegionSelector(Dispatcher dispatcher, IHotkeyService hotkeys) : IRegionSelector
{
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
        var bounds = overlays.Select(o => o.MonitorBounds).ToList();
        var active = 0;

        void OnKey(HotkeyBinding key)
        {
            var overlay = overlays[active];
            switch (RegionKeyboard.Map(key, overlay.KeyboardStep))
            {
                case { Action: RegionKeyAction.Cancel }:
                    result.TrySetResult(null);
                    break;
                case { Action: RegionKeyAction.Confirm }:
                    result.TrySetResult(overlay.CurrentOrFullSelection());
                    break;
                case { Action: RegionKeyAction.NextMonitor or RegionKeyAction.PreviousMonitor } command:
                    var next = RegionKeyboard.Cycle(bounds, active, backwards: command.Action == RegionKeyAction.PreviousMonitor);
                    if (next != active)
                    {
                        active = next;
                        overlays[active].Activate();
                        overlays[active].ApplyKeyboard(command with { Action = RegionKeyAction.Move });
                    }
                    break;
                case { } command:
                    overlay.ApplyKeyboard(command);
                    break;
            }
        }

        for (var i = 0; i < overlays.Count; i++)
        {
            var index = i;
            var overlay = overlays[i];
            overlay.Completed += (_, selection) => result.TrySetResult(selection);
            overlay.Cancelled += (_, _) => result.TrySetResult(null);
            overlay.KeyPressed += (_, key) => OnKey(key);
            overlay.SelectionStarted += (_, _) =>
            {
                active = index;
                foreach (var other in overlays.Where(o => !ReferenceEquals(o, overlay)))
                {
                    other.ClearSelection();
                }
            };
        }

        void OnHotkey(HotkeyBinding key) => dispatcher.BeginInvoke(() => OnKey(key));
        using var singleKeys = hotkeys.RegisterTemporary(RegionKeyboard.SingleKeys, OnHotkey);
        using var repeatingKeys = hotkeys.RegisterTemporary(RegionKeyboard.RepeatingKeys, OnHotkey, repeat: true);
        await using var cancellation = cancellationToken.Register(() => result.TrySetResult(null));

        foreach (var overlay in overlays)
        {
            overlay.Show();
        }
        active = Math.Max(0, overlays.FindIndex(o => o.IsMouseOver));
        overlays[active].Activate();

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
