// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Windows;
using System.Windows.Interop;
using CaptureImage.Core.Capture;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace CaptureImage.UI.Interop;

/// <summary>Win32 window tweaks WPF has no property for.</summary>
internal static class WindowInterop
{
    /// <summary>
    /// Keep the window out of screenshots and screen recordings (toasts, overlays, pinned
    /// thumbnails must never end up in the user's captures).
    /// </summary>
    public static void ExcludeFromCapture(Window window)
    {
        var hwnd = GetHandle(window);
        if (hwnd != HWND.Null)
        {
            PInvoke.SetWindowDisplayAffinity(hwnd, WINDOW_DISPLAY_AFFINITY.WDA_EXCLUDEFROMCAPTURE);
        }
    }

    /// <summary>A tool window that is never activated by clicks — it can't steal focus from a game.</summary>
    public static void MakeNonActivating(Window window)
    {
        var hwnd = GetHandle(window);
        if (hwnd == HWND.Null)
        {
            return;
        }

        var style = (WINDOW_EX_STYLE)PInvoke.GetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        style |= WINDOW_EX_STYLE.WS_EX_NOACTIVATE | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW;
        PInvoke.SetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (nint)style);
    }

    /// <summary>Place the window exactly over <paramref name="bounds"/> (physical pixels), topmost.</summary>
    public static void SetPhysicalBounds(Window window, PixelRect bounds)
    {
        var hwnd = GetHandle(window);
        if (hwnd != HWND.Null)
        {
            PInvoke.SetWindowPos(
                hwnd,
                HWND.HWND_TOPMOST,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);
        }
    }

    /// <summary>Restore and bring a window to the foreground (another instance asked for it).</summary>
    public static void BringToFront(Window window)
    {
        var hwnd = GetHandle(window);
        if (hwnd == HWND.Null)
        {
            return;
        }

        if (PInvoke.IsIconic(hwnd))
        {
            PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_RESTORE);
        }
        PInvoke.SetForegroundWindow(hwnd);
    }

    private static HWND GetHandle(Window window) => new(new WindowInteropHelper(window).EnsureHandle());
}
