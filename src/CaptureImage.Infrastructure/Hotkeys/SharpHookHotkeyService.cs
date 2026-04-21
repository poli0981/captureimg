using System.Runtime.Versioning;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using Microsoft.Extensions.Logging;
using SharpHook;
using SharpHook.Native;

namespace CaptureImage.Infrastructure.Hotkeys;

/// <summary>
/// <see cref="IHotkeyService"/> implementation backed by SharpHook's
/// <see cref="TaskPoolGlobalHook"/>. The hook runs on a background thread pool; subscribers
/// see events from arbitrary threads and must marshal to the UI via
/// <see cref="IUIThreadDispatcher"/>.
/// </summary>
/// <remarks>
/// <para>
/// We match on the native <c>RawCode</c> (Win32 VK on Windows) rather than SharpHook's
/// <see cref="KeyCode"/> enum because our <see cref="HotkeyBinding"/> persists VK codes —
/// round-trip is lossless and maps 1:1 to the platform.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows10.0.22621.0")]
public sealed class SharpHookHotkeyService : IHotkeyService
{
    private readonly ILogger<SharpHookHotkeyService> _logger;
    private readonly object _gate = new();
    private TaskPoolGlobalHook? _hook;
    private HotkeyBinding? _binding;
    private bool _disposed;

    public SharpHookHotkeyService(ILogger<SharpHookHotkeyService> logger)
    {
        _logger = logger;
    }

    public HotkeyBinding? CurrentBinding
    {
        get { lock (_gate) return _binding; }
    }

    public event EventHandler? Triggered;

    public void SetBinding(HotkeyBinding binding)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _binding = binding;
            EnsureHookRunning_NoLock();
            _logger.LogInformation("Hotkey binding set to {Binding}", binding);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _binding = null;
            if (_hook is not null)
            {
                try
                {
                    _hook.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to dispose SharpHook on Stop.");
                }
                _hook = null;
                _logger.LogInformation("Hotkey service stopped.");
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                _hook?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispose SharpHook on Dispose.");
            }
            _hook = null;
        }
    }

    private void EnsureHookRunning_NoLock()
    {
        if (_hook is not null) return;

        _hook = new TaskPoolGlobalHook();
        _hook.KeyPressed += OnKeyPressed;
        _hook.RunAsync();
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        HotkeyBinding? binding;
        lock (_gate)
        {
            binding = _binding;
        }
        if (binding is null) return;

        // libuiohook exposes the native VK on Windows in RawCode.
        if ((uint)e.Data.RawCode != binding.Value.VirtualKey)
        {
            return;
        }

        var mask = e.RawEvent.Mask;
        var ctrl  = (mask & ModifierMask.Ctrl)  != 0;
        var alt   = (mask & ModifierMask.Alt)   != 0;
        var shift = (mask & ModifierMask.Shift) != 0;
        var meta  = (mask & ModifierMask.Meta)  != 0;

        var wantCtrl  = binding.Value.Modifiers.HasFlag(HotkeyModifiers.Control);
        var wantAlt   = binding.Value.Modifiers.HasFlag(HotkeyModifiers.Alt);
        var wantShift = binding.Value.Modifiers.HasFlag(HotkeyModifiers.Shift);
        var wantWin   = binding.Value.Modifiers.HasFlag(HotkeyModifiers.Win);

        if (ctrl != wantCtrl || alt != wantAlt || shift != wantShift || meta != wantWin)
        {
            return;
        }

        try
        {
            Triggered?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hotkey Triggered handler threw.");
        }
    }
}
