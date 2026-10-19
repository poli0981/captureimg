// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Abstractions;
using CaptureImage.Infrastructure.Win32;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace CaptureImage.Infrastructure.Windowing;

/// <summary>
/// Watches top-level windows with out-of-context WinEvent hooks (create, destroy, show,
/// hide, rename, minimize, foreground). Replaces the v1 WMI watcher, which kept the WMI
/// service polling the process list every two seconds forever.
/// </summary>
/// <remarks>
/// Hooks are installed and removed on a dedicated message-loop thread (they are
/// thread-affine), and the callback delegate is held in a field so the GC can't collect it
/// while Windows still calls it. List changes are debounced; foreground changes are
/// forwarded immediately. Both events are raised on the thread pool.
/// </remarks>
public sealed unsafe partial class WinEventWindowWatcher : IWindowWatcher
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(400);

    private readonly ILogger<WinEventWindowWatcher> _logger;
    private readonly Lock _gate = new();
    private readonly List<HWINEVENTHOOK> _hooks = [];
    private readonly WINEVENTPROC _callback;
    private readonly Timer _debounce;
    private MessageLoopThread? _loop;
    private HWND _lastForeground;
    private bool _running;
    private bool _disposed;

    public WinEventWindowWatcher(ILogger<WinEventWindowWatcher> logger)
    {
        _logger = logger;
        _callback = OnWinEvent;
        _debounce = new Timer(_ => WindowsChanged?.Invoke(this, EventArgs.Empty), null, Timeout.Infinite, Timeout.Infinite);
    }

    public event EventHandler? WindowsChanged;

    public event EventHandler<nint>? ForegroundChanged;

    public bool IsRunning
    {
        get { lock (_gate) return _running; }
    }

    public void Start()
    {
        MessageLoopThread loop;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_running)
            {
                return;
            }
            _running = true;
            loop = _loop ??= new MessageLoopThread("CaptureImage window events");
        }

        loop.Invoke(() =>
        {
            Hook(PInvoke.EVENT_SYSTEM_FOREGROUND, PInvoke.EVENT_SYSTEM_FOREGROUND);
            Hook(PInvoke.EVENT_SYSTEM_MINIMIZESTART, PInvoke.EVENT_SYSTEM_MINIMIZEEND);
            Hook(PInvoke.EVENT_OBJECT_CREATE, PInvoke.EVENT_OBJECT_HIDE);
            Hook(PInvoke.EVENT_OBJECT_NAMECHANGE, PInvoke.EVENT_OBJECT_NAMECHANGE);
        });
        LogStarted(_hooks.Count);
    }

    public void Stop()
    {
        MessageLoopThread? loop;
        lock (_gate)
        {
            if (!_running)
            {
                return;
            }
            _running = false;
            loop = _loop;
        }

        loop?.Invoke(() =>
        {
            foreach (var hook in _hooks)
            {
                PInvoke.UnhookWinEvent(hook);
            }
            _hooks.Clear();
        });
        _debounce.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        lock (_gate)
        {
            _disposed = true;
        }
        _loop?.Dispose();
        _debounce.Dispose();
    }

    private void Hook(uint eventMin, uint eventMax)
    {
        var hook = PInvoke.SetWinEventHook(
            eventMin,
            eventMax,
            HMODULE.Null,
            _callback,
            0,
            0,
            PInvoke.WINEVENT_OUTOFCONTEXT | PInvoke.WINEVENT_SKIPOWNPROCESS);
        if (hook.IsNull)
        {
            LogHookFailed(eventMin, eventMax);
            return;
        }
        _hooks.Add(hook);
    }

    private void OnWinEvent(HWINEVENTHOOK hook, uint @event, HWND hwnd, int idObject, int idChild, uint eventThread, uint eventTime)
    {
        if (hwnd.IsNull || idObject != (int)OBJECT_IDENTIFIER.OBJID_WINDOW || idChild != (int)PInvoke.CHILDID_SELF)
        {
            return;
        }

        if (@event == PInvoke.EVENT_SYSTEM_FOREGROUND)
        {
            if (hwnd == _lastForeground)
            {
                return;
            }
            _lastForeground = hwnd;
            var handle = (nint)hwnd.Value;
            ThreadPool.QueueUserWorkItem(_ => ForegroundChanged?.Invoke(this, handle));
            return;
        }

        // Ignore child windows. A destroyed window can no longer be asked for its root, so
        // destroy events always count.
        if (@event != PInvoke.EVENT_OBJECT_DESTROY && PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT) != hwnd)
        {
            return;
        }

        _debounce.Change(Debounce, Timeout.InfiniteTimeSpan);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Window watcher started with {Count} WinEvent hooks.")]
    private partial void LogStarted(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SetWinEventHook failed for events 0x{Min:X}-0x{Max:X}.")]
    private partial void LogHookFailed(uint min, uint max);
}
