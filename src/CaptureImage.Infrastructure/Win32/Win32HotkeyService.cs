// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace CaptureImage.Infrastructure.Win32;

/// <summary>
/// Global hotkeys through Win32 <c>RegisterHotKey</c> — no keyboard hook, so no callback runs
/// for every key press on the machine and the keys keep working while an elevated game has
/// focus. Registration failures are reported per mode instead of being guessed.
/// </summary>
/// <remarks>
/// A registered combination is consumed by Windows and never reaches other applications,
/// which is why bindings are only registered while the coordinator is armed.
/// <c>MOD_NOREPEAT</c> is always added so holding a key fires once.
/// </remarks>
public sealed partial class Win32HotkeyService : IHotkeyService
{
    private const int FirstCaptureId = 0x0100;
    private const int ProbeId = 0x01FF;
    private const int FirstTemporaryId = 0x0200;

    private readonly MessageLoopThread _loop;
    private readonly ILogger<Win32HotkeyService> _logger;
    private readonly Lock _gate = new();
    private readonly Dictionary<int, CaptureMode> _captureIds = [];
    private readonly Dictionary<int, (HotkeyBinding Binding, Action<HotkeyBinding> Callback)> _temporaryIds = [];
    private IReadOnlyList<HotkeyRegistration> _registrations = [];
    private int _nextTemporaryId = FirstTemporaryId;
    private bool _disposed;

    public Win32HotkeyService(ILogger<Win32HotkeyService> logger)
    {
        _logger = logger;
        _loop = new MessageLoopThread("CaptureImage hotkeys");
        _loop.HotkeyPressed += OnHotkeyPressed;
    }

    public event EventHandler<CaptureMode>? Triggered;

    public bool IsActive
    {
        get { lock (_gate) return _captureIds.Count > 0; }
    }

    public IReadOnlyList<HotkeyRegistration> Registrations
    {
        get { lock (_gate) return _registrations; }
    }

    public IReadOnlyList<HotkeyRegistration> Activate(HotkeySettings bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = _loop.Invoke(() =>
        {
            UnregisterCaptureKeys_OnLoop();
            var registrations = new List<HotkeyRegistration>(3);
            var id = FirstCaptureId;
            foreach (var mode in Enum.GetValues<CaptureMode>())
            {
                var binding = bindings.Get(mode);
                var status = Register_OnLoop(id, binding);
                if (status == HotkeyRegistrationStatus.Registered)
                {
                    lock (_gate)
                    {
                        _captureIds[id] = mode;
                    }
                }
                registrations.Add(new HotkeyRegistration(mode, binding, status));
                id++;
            }
            return registrations;
        });

        lock (_gate)
        {
            _registrations = result;
        }

        foreach (var registration in result.Where(r => r.Status is HotkeyRegistrationStatus.Conflict or HotkeyRegistrationStatus.Invalid))
        {
            LogNotRegistered(registration.Mode, registration.Binding.ToString(), registration.Status);
        }
        return result;
    }

    public void Deactivate()
    {
        if (_disposed)
        {
            return;
        }

        _loop.Invoke(UnregisterCaptureKeys_OnLoop);
        lock (_gate)
        {
            _registrations = [];
        }
    }

    public bool IsTakenByAnotherApp(HotkeyBinding binding)
    {
        if (binding.IsUnbound || !binding.IsValid() || _disposed)
        {
            return false;
        }

        lock (_gate)
        {
            if (_registrations.Any(r => r.Status == HotkeyRegistrationStatus.Registered && r.Binding == binding))
            {
                return false; // ours
            }
        }

        return _loop.Invoke(() =>
        {
            if (Register_OnLoop(ProbeId, binding) != HotkeyRegistrationStatus.Registered)
            {
                return true;
            }
            PInvoke.UnregisterHotKey(HWND.Null, ProbeId);
            return false;
        });
    }

    public IDisposable RegisterTemporary(IReadOnlyList<HotkeyBinding> bindings, Action<HotkeyBinding> callback)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(callback);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var ids = _loop.Invoke(() =>
        {
            var registered = new List<int>(bindings.Count);
            foreach (var binding in bindings)
            {
                var id = _nextTemporaryId++;
                if (PInvoke.RegisterHotKey(HWND.Null, id, ToModifiers(binding.Modifiers), binding.VirtualKey))
                {
                    lock (_gate)
                    {
                        _temporaryIds[id] = (binding, callback);
                    }
                    registered.Add(id);
                }
            }
            return registered;
        });

        return new TemporaryRegistration(this, ids);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        _loop.Invoke(() =>
        {
            UnregisterCaptureKeys_OnLoop();
            int[] temporary;
            lock (_gate)
            {
                temporary = [.. _temporaryIds.Keys];
                _temporaryIds.Clear();
            }
            foreach (var id in temporary)
            {
                PInvoke.UnregisterHotKey(HWND.Null, id);
            }
        });
        _loop.HotkeyPressed -= OnHotkeyPressed;
        _loop.Dispose();
    }

    private void ReleaseTemporary(IReadOnlyList<int> ids)
    {
        if (_disposed)
        {
            return;
        }

        _loop.Invoke(() =>
        {
            foreach (var id in ids)
            {
                lock (_gate)
                {
                    _temporaryIds.Remove(id);
                }
                PInvoke.UnregisterHotKey(HWND.Null, id);
            }
        });
    }

    private HotkeyRegistrationStatus Register_OnLoop(int id, HotkeyBinding binding)
    {
        if (binding.IsUnbound)
        {
            return HotkeyRegistrationStatus.Unbound;
        }

        if (!binding.IsValid())
        {
            return HotkeyRegistrationStatus.Invalid;
        }

        return PInvoke.RegisterHotKey(HWND.Null, id, ToModifiers(binding.Modifiers), binding.VirtualKey)
            ? HotkeyRegistrationStatus.Registered
            : HotkeyRegistrationStatus.Conflict;
    }

    private void UnregisterCaptureKeys_OnLoop()
    {
        int[] ids;
        lock (_gate)
        {
            ids = [.. _captureIds.Keys];
            _captureIds.Clear();
        }

        foreach (var id in ids)
        {
            PInvoke.UnregisterHotKey(HWND.Null, id);
        }
    }

    private void OnHotkeyPressed(int id)
    {
        CaptureMode? mode = null;
        (HotkeyBinding Binding, Action<HotkeyBinding> Callback)? temporary = null;
        lock (_gate)
        {
            if (_captureIds.TryGetValue(id, out var m))
            {
                mode = m;
            }
            else if (_temporaryIds.TryGetValue(id, out var t))
            {
                temporary = t;
            }
        }

        // Never run subscriber code on the message loop: a slow handler would delay the next key.
        if (mode is { } captureMode)
        {
            LogPressed(captureMode);
            ThreadPool.QueueUserWorkItem(_ => Triggered?.Invoke(this, captureMode));
        }
        else if (temporary is { } t)
        {
            ThreadPool.QueueUserWorkItem(_ => t.Callback(t.Binding));
        }
    }

    private static HOT_KEY_MODIFIERS ToModifiers(HotkeyModifiers modifiers) =>
        (HOT_KEY_MODIFIERS)(uint)modifiers | HOT_KEY_MODIFIERS.MOD_NOREPEAT;

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Mode} hotkey {Binding} not registered: {Status}.")]
    private partial void LogNotRegistered(CaptureMode mode, string binding, HotkeyRegistrationStatus status);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{Mode} hotkey pressed.")]
    private partial void LogPressed(CaptureMode mode);

    private sealed class TemporaryRegistration(Win32HotkeyService owner, IReadOnlyList<int> ids) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                owner.ReleaseTemporary(ids);
            }
        }
    }
}
