using System;
using CaptureImage.Core.Models;

namespace CaptureImage.Core.Abstractions;

/// <summary>
/// Global (system-wide) hotkey listener. Subscribers are responsible for marshalling
/// to the UI thread — <see cref="Triggered"/> fires from whatever thread the hook uses.
/// </summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>Currently registered binding, or <c>null</c> if the service is stopped.</summary>
    HotkeyBinding? CurrentBinding { get; }

    /// <summary>Fired whenever the registered binding matches the currently pressed keys.</summary>
    event EventHandler? Triggered;

    /// <summary>
    /// Start listening with the given binding. If the service is already listening, the
    /// previous binding is replaced.
    /// </summary>
    void SetBinding(HotkeyBinding binding);

    /// <summary>Stop listening. Safe to call multiple times.</summary>
    void Stop();
}
