// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.WindowsAndMessaging;

namespace CaptureImage.Infrastructure.Windowing;

/// <summary>
/// Enumerates capturable top-level windows and describes them as <see cref="GameTarget"/>s.
/// Runs off the UI thread; executable paths come from <c>QueryFullProcessImageName</c>, which
/// also works for elevated processes (v1 used <c>Process.MainModule</c>, which throws for
/// games running as administrator and left them without icon or Steam badge).
/// </summary>
public sealed unsafe partial class TargetCatalog(
    IconExtractor icons,
    ISteamDetector steam,
    ILogger<TargetCatalog> logger) : ITargetCatalog
{
    private static readonly HashSet<string> ShellWindowClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow",
    };

    private readonly IconExtractor _icons = icons;
    private readonly ISteamDetector _steam = steam;
    private readonly ILogger<TargetCatalog> _logger = logger;
    private readonly uint _ownProcessId = (uint)Environment.ProcessId;

    public Task<IReadOnlyList<GameTarget>> EnumerateAsync(CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<GameTarget>>(() =>
        {
            var handles = new List<HWND>(128);
            PInvoke.EnumWindows((hwnd, _) =>
            {
                handles.Add(hwnd);
                return true;
            }, default);

            var targets = new List<GameTarget>(handles.Count);
            foreach (var hwnd in handles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (TryDescribe(hwnd) is { } target)
                {
                    targets.Add(target);
                }
            }

            LogEnumerated(targets.Count, handles.Count);
            return targets;
        }, cancellationToken);

    public Task<GameTarget?> GetTargetAsync(nint windowHandle, CancellationToken cancellationToken = default) =>
        windowHandle == 0
            ? Task.FromResult<GameTarget?>(null)
            : Task.Run(() => TryDescribe(new HWND(windowHandle)), cancellationToken);

    public nint GetForegroundWindow() => (nint)PInvoke.GetForegroundWindow().Value;

    private GameTarget? TryDescribe(HWND hwnd)
    {
        if (!IsCapturableTopLevel(hwnd, out var processId))
        {
            return null;
        }

        var title = GetTitle(hwnd);
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var path = GetExecutablePath(processId);
        var processName = string.IsNullOrEmpty(path) ? GetProcessNameFallback(processId) : Path.GetFileNameWithoutExtension(path);

        return new GameTarget(
            processId,
            (nint)hwnd.Value,
            processName,
            title,
            path,
            string.IsNullOrEmpty(path) ? null : _icons.TryGetIconPngBytes(path),
            string.IsNullOrEmpty(path) ? null : _steam.TryGetAppInfo(path),
            PInvoke.IsIconic(hwnd));
    }

    private unsafe bool IsCapturableTopLevel(HWND hwnd, out uint processId)
    {
        processId = 0;
        if (!PInvoke.IsWindow(hwnd) || !PInvoke.IsWindowVisible(hwnd))
        {
            return false;
        }

        if (!PInvoke.GetWindow(hwnd, GET_WINDOW_CMD.GW_OWNER).IsNull)
        {
            return false; // dialogs and pop-ups owned by another window
        }

        var exStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        if (exStyle.HasFlag(WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) && !exStyle.HasFlag(WINDOW_EX_STYLE.WS_EX_APPWINDOW))
        {
            return false;
        }

        // Cloaked windows (other virtual desktops, suspended UWP frames) never produce frames.
        uint cloaked = 0;
        if (PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint)).Succeeded && cloaked != 0)
        {
            return false;
        }

        if (ShellWindowClasses.Contains(GetClassName(hwnd)))
        {
            return false;
        }

        uint pid;
        _ = PInvoke.GetWindowThreadProcessId(hwnd, &pid);
        processId = pid;
        return pid != 0 && pid != _ownProcessId;
    }

    private static unsafe string GetTitle(HWND hwnd)
    {
        var length = PInvoke.GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        var buffer = new char[length + 1];
        fixed (char* p = buffer)
        {
            var copied = PInvoke.GetWindowText(hwnd, p, buffer.Length);
            return new string(buffer, 0, copied);
        }
    }

    private static unsafe string GetClassName(HWND hwnd)
    {
        var buffer = new char[256];
        fixed (char* p = buffer)
        {
            var copied = PInvoke.GetClassName(hwnd, p, buffer.Length);
            return copied > 0 ? new string(buffer, 0, copied) : string.Empty;
        }
    }

    private static string GetExecutablePath(uint processId)
    {
        using var process = PInvoke.OpenProcess_SafeHandle(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (process.IsInvalid)
        {
            return string.Empty;
        }

        var buffer = new char[1024];
        var size = (uint)buffer.Length;
        return PInvoke.QueryFullProcessImageName(process, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, buffer, ref size)
            ? new string(buffer, 0, (int)size)
            : string.Empty;
    }

    private static string GetProcessNameFallback(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception or ExternalException)
        {
            return processId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Enumerated {Count} capturable windows out of {Total} top-level windows.")]
    private partial void LogEnumerated(int count, int total);
}
