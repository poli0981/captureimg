using System.Threading;
using CaptureImage.Core.Abstractions;
using CaptureImage.Infrastructure.Processes.Interop;
using Microsoft.Extensions.Logging;

namespace CaptureImage.Infrastructure.Processes;

/// <summary>
/// Win32 implementation of <see cref="IForegroundWindowWatcher"/>. Wraps
/// <c>SetWinEventHook(EVENT_SYSTEM_FOREGROUND, …)</c> and debounces rapid switches so
/// the dashboard only re-selects the target after the user lands on a window.
/// </summary>
public sealed class Win32ForegroundWindowWatcher : IForegroundWindowWatcher
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(250);

    private readonly ILogger<Win32ForegroundWindowWatcher> _logger;
    private readonly object _gate = new();
    private readonly uint _ownProcessId;

    private IntPtr _hookHandle;
    // Holding a strong reference to the delegate is mandatory: the callback is invoked
    // by the OS message loop, and a GC of the delegate while the hook is active
    // crashes with an AccessViolationException on the next foreground change.
    private User32.WinEventDelegate? _callback;

    private readonly Timer _debounceTimer;
    private IntPtr _pendingHwnd;
    private bool _disposed;

    public event EventHandler<nint>? ForegroundChanged;

    public bool IsRunning { get; private set; }

    public Win32ForegroundWindowWatcher(ILogger<Win32ForegroundWindowWatcher> logger)
    {
        _logger = logger;
        _ownProcessId = (uint)Environment.ProcessId;
        _debounceTimer = new Timer(OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (IsRunning) return;

            _callback = OnRawForegroundEvent;
            _hookHandle = User32.SetWinEventHook(
                User32.EVENT_SYSTEM_FOREGROUND, User32.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _callback,
                idProcess: 0, idThread: 0,
                User32.WINEVENT_OUTOFCONTEXT | User32.WINEVENT_SKIPOWNPROCESS);

            if (_hookHandle == IntPtr.Zero)
            {
                _logger.LogWarning("Foreground window watcher failed to register WinEvent hook.");
                _callback = null;
                return;
            }

            IsRunning = true;
            _logger.LogDebug("Foreground window watcher started (hook=0x{Hook:X}).", _hookHandle);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (_hookHandle != IntPtr.Zero)
            {
                User32.UnhookWinEvent(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
            _callback = null;
            _pendingHwnd = IntPtr.Zero;
            _debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
            if (IsRunning)
            {
                _logger.LogDebug("Foreground window watcher stopped.");
            }
            IsRunning = false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _debounceTimer.Dispose();
    }

    private void OnRawForegroundEvent(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // Use GetForegroundWindow() rather than the event's hwnd. On Win11 Alt-Tab the
        // event-supplied hwnd can be the modern Task View overlay (idObject != 0) or a
        // transient cloaked window — which got the original watcher to filter the event
        // out and miss the actual destination. Polling the OS at callback time always
        // returns the real foreground HWND. Mouse-click activations happened to ship a
        // single clean event so they worked under the old code; Alt-Tab didn't.
        var foreground = User32.GetForegroundWindow();
        if (foreground == IntPtr.Zero) return;

        // WINEVENT_SKIPOWNPROCESS already filters most own-window events, but a defensive
        // pid check covers edge cases (e.g. a brokered window owned by another process).
        User32.GetWindowThreadProcessId(foreground, out var pid);
        if (pid == _ownProcessId) return;
        if (pid == 0) return;

        _pendingHwnd = foreground;
        _debounceTimer.Change(DebounceDelay, Timeout.InfiniteTimeSpan);
    }

    private void OnDebounceElapsed(object? state)
    {
        IntPtr hwnd;
        lock (_gate)
        {
            if (_disposed || !IsRunning) return;
            hwnd = _pendingHwnd;
            _pendingHwnd = IntPtr.Zero;
        }
        if (hwnd == IntPtr.Zero) return;

        _logger.LogDebug("Foreground window watcher firing for HWND 0x{Hwnd:X}.", hwnd);

        try
        {
            ForegroundChanged?.Invoke(this, hwnd);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ForegroundChanged subscriber threw.");
        }
    }
}
