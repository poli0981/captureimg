using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using CaptureImage.Infrastructure.Processes.Interop;
using Microsoft.Extensions.Logging;

namespace CaptureImage.Infrastructure.Processes;

/// <summary>
/// Raw result of a single <c>EnumWindows</c> iteration — a top-level window that passed
/// the visibility + tool-window filter, together with its owning process identity.
/// </summary>
public sealed record WindowInfo(
    IntPtr Hwnd,
    uint ProcessId,
    string WindowTitle);

/// <summary>
/// Enumerates visible top-level windows that are candidates for capture. Filters out
/// tool windows, owned windows, and the current process.
/// </summary>
public sealed class WindowEnumerator
{
    private readonly ILogger<WindowEnumerator> _logger;

    public WindowEnumerator(ILogger<WindowEnumerator> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<WindowInfo> Enumerate()
    {
        var selfPid = (uint)Environment.ProcessId;
        var results = new List<WindowInfo>(capacity: 32);

        bool Callback(IntPtr hwnd, IntPtr _)
        {
            try
            {
                if (!User32.IsWindowVisible(hwnd)) return true;

                var length = User32.GetWindowTextLength(hwnd);
                if (length <= 0) return true;

                // Reject tool windows unless they explicitly opt into WS_EX_APPWINDOW.
                var exStyle = (long)User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
                if ((exStyle & User32.WS_EX_TOOLWINDOW) != 0 &&
                    (exStyle & User32.WS_EX_APPWINDOW) == 0)
                {
                    return true;
                }

                // Reject owned windows (dialogs, popups).
                if (User32.GetWindow(hwnd, User32.GW_OWNER) != IntPtr.Zero)
                {
                    return true;
                }

                if (User32.GetWindowThreadProcessId(hwnd, out var pid) == 0 || pid == 0)
                {
                    return true;
                }

                // Skip ourselves.
                if (pid == selfPid) return true;

                var title = ReadWindowTitle(hwnd, length);
                if (string.IsNullOrWhiteSpace(title)) return true;

                results.Add(new WindowInfo(hwnd, pid, title));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "EnumWindows callback failed for HWND {Hwnd}.", hwnd);
            }
            return true; // always continue enumeration
        }

        if (!User32.EnumWindows(Callback, IntPtr.Zero))
        {
            _logger.LogWarning("EnumWindows returned FALSE (last error {Err}).",
                System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        }
        return results;
    }

    private static string ReadWindowTitle(IntPtr hwnd, int length)
    {
        var buffer = new StringBuilder(length + 1);
        _ = User32.GetWindowText(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    /// <summary>
    /// Resolve the absolute executable path for <paramref name="processId"/>, or empty string
    /// if access is denied or the process has already exited.
    /// </summary>
    public string TryGetExecutablePath(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.MainModule?.FileName ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read executable path for PID {Pid}.", processId);
            return string.Empty;
        }
    }

    /// <summary>
    /// Resolve the short process name (no extension) for <paramref name="processId"/>,
    /// or empty string if the process has already exited.
    /// </summary>
    public string TryGetProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }
}
