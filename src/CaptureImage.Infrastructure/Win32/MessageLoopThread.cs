// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Collections.Concurrent;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace CaptureImage.Infrastructure.Win32;

/// <summary>
/// A background thread running a Win32 message loop. Thread-affine APIs live here:
/// <c>RegisterHotKey(NULL, …)</c> posts <c>WM_HOTKEY</c> to this thread's queue, and
/// out-of-context WinEvent hooks deliver their callbacks while this loop pumps.
/// </summary>
/// <remarks>
/// The thread is a background thread so it never keeps the process alive, and the loop
/// exits on <c>WM_QUIT</c> after running any queued clean-up work.
/// </remarks>
internal sealed class MessageLoopThread : IDisposable
{
    private const uint WmInvoke = PInvoke.WM_APP + 1;

    private readonly Thread _thread;
    private readonly ConcurrentQueue<Action> _work = new();
    private readonly uint _threadId;
    private int _disposed;

    public MessageLoopThread(string name)
    {
        var started = new TaskCompletionSource<uint>(TaskCreationOptions.RunContinuationsAsynchronously);
        _thread = new Thread(() => Run(started)) { IsBackground = true, Name = name };
        _thread.Start();
        _threadId = started.Task.GetAwaiter().GetResult();
    }

    /// <summary>Raised on the loop thread with the hotkey id from <c>WM_HOTKEY</c>. Keep handlers short.</summary>
    public event Action<int>? HotkeyPressed;

    public bool IsCurrentThread => Environment.CurrentManagedThreadId == _thread.ManagedThreadId;

    /// <summary>Run <paramref name="func"/> on the loop thread and wait for the result.</summary>
    public T Invoke<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        if (IsCurrentThread)
        {
            return func();
        }

        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(() =>
        {
            try
            {
                completion.SetResult(func());
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });
        return completion.Task.GetAwaiter().GetResult();
    }

    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Invoke(() =>
        {
            action();
            return 0;
        });
    }

    /// <summary>Queue <paramref name="action"/> on the loop thread without waiting.</summary>
    public void Post(Action action)
    {
        _work.Enqueue(action);
        PInvoke.PostThreadMessage(_threadId, WmInvoke, default, default);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        PInvoke.PostThreadMessage(_threadId, PInvoke.WM_QUIT, default, default);
        if (!IsCurrentThread)
        {
            _thread.Join(TimeSpan.FromSeconds(2));
        }
    }

    private void Run(TaskCompletionSource<uint> started)
    {
        // The first PeekMessage call creates this thread's message queue, so messages posted
        // right after we report the thread id are not lost.
        PInvoke.PeekMessage(out _, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE);
        started.SetResult(PInvoke.GetCurrentThreadId());

        while (true)
        {
            var result = PInvoke.GetMessage(out var msg, HWND.Null, 0, 0);
            if (result.Value <= 0)
            {
                break; // WM_QUIT (0) or error (-1)
            }

            if (msg.hwnd == HWND.Null && msg.message == WmInvoke)
            {
                Drain();
                continue;
            }

            if (msg.hwnd == HWND.Null && msg.message == PInvoke.WM_HOTKEY)
            {
                HotkeyPressed?.Invoke((int)msg.wParam.Value);
                continue;
            }

            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }

        Drain();
    }

    private void Drain()
    {
        while (_work.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Posted work reports its own failures (Invoke forwards them to the caller);
                // an escaped exception must not take the message loop — and the process — down.
                System.Diagnostics.Trace.TraceError("MessageLoopThread work item failed: {0}", ex);
            }
        }
    }
}
