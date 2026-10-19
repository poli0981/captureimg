// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;

namespace CaptureImage.Core.Abstractions;

/// <summary>A frozen picture of one monitor, shown by the region selector.</summary>
/// <param name="Monitor">The monitor that was captured.</param>
/// <param name="DisplayFrame">An 8-bit rendition of the monitor (tone-mapped when it was HDR).</param>
public sealed record MonitorSnapshot(MonitorInfo Monitor, CapturedFrame DisplayFrame);

/// <summary>The rectangle a user dragged, relative to the monitor's top-left corner (physical pixels).</summary>
public sealed record RegionSelection(MonitorInfo Monitor, PixelRect Area);

/// <summary>Full-screen overlays (one per monitor) for dragging a capture rectangle.</summary>
public interface IRegionSelector
{
    /// <summary>The user's selection, or <c>null</c> when they cancelled (Esc, right click).</summary>
    Task<RegionSelection?> SelectAsync(IReadOnlyList<MonitorSnapshot> snapshots, CancellationToken cancellationToken = default);
}

/// <summary>"Save this capture?" dialog shown when preview-before-save is enabled.</summary>
public interface IPreviewPresenter
{
    /// <summary>True to save, false to discard.</summary>
    Task<bool> ShowAsync(CapturedFrame frame, string title, CancellationToken cancellationToken = default);
}

/// <summary>Floating always-on-top thumbnails of recent captures.</summary>
public interface IPinnedThumbnailHost
{
    void Show(CapturedFrame frame, string? filePath, string title);
}

public interface IClipboardService
{
    Task<bool> CopyImageAsync(CapturedFrame frame, CancellationToken cancellationToken = default);

    Task<bool> CopyTextAsync(string text, CancellationToken cancellationToken = default);
}

/// <summary>
/// Notifications. While the main window is visible they appear inside it; while the app sits
/// in the tray they appear as small non-activating pop-ups that never steal focus from a game.
/// </summary>
public interface IToastService
{
    void Show(ToastKind kind, string title, string message, TimeSpan? duration = null);
}

public static class ToastServiceExtensions
{
    public static void ShowSuccess(this IToastService toasts, string title, string message) =>
        toasts.Show(ToastKind.Success, title, message);

    public static void ShowError(this IToastService toasts, string title, string message) =>
        toasts.Show(ToastKind.Error, title, message);

    public static void ShowInfo(this IToastService toasts, string title, string message) =>
        toasts.Show(ToastKind.Info, title, message);

    public static void ShowWarning(this IToastService toasts, string title, string message) =>
        toasts.Show(ToastKind.Warning, title, message);
}

/// <summary>Marshals work onto the UI thread.</summary>
public interface IUIThreadDispatcher
{
    bool IsOnUIThread { get; }

    /// <summary>Queue <paramref name="action"/>; runs inline when already on the UI thread.</summary>
    void Post(Action action);

    Task InvokeAsync(Action action);

    Task<T> InvokeAsync<T>(Func<T> func);

    /// <summary>Run an async function on the UI thread and observe its completion.</summary>
    Task InvokeAsync(Func<Task> func);
}
