// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;

namespace CaptureImage.Core.Abstractions;

/// <summary>Result of trying to register one global hotkey.</summary>
public enum HotkeyRegistrationStatus
{
    /// <summary>The mode has no hotkey.</summary>
    Unbound,

    /// <summary>Registered with Windows; it fires while the service is active.</summary>
    Registered,

    /// <summary>Another application already owns the combination.</summary>
    Conflict,

    /// <summary>The binding is not a valid hotkey (see <see cref="HotkeyBinding.Validate"/>).</summary>
    Invalid,
}

public sealed record HotkeyRegistration(CaptureMode Mode, HotkeyBinding Binding, HotkeyRegistrationStatus Status);

/// <summary>
/// System-wide hotkeys (Win32 <c>RegisterHotKey</c>). Keys are only registered while the
/// service is active, because a registered combination never reaches other applications.
/// </summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>A capture hotkey was pressed. Raised on a background thread.</summary>
    event EventHandler<CaptureMode>? Triggered;

    bool IsActive { get; }

    IReadOnlyList<HotkeyRegistration> Registrations { get; }

    /// <summary>(Re)register every bound mode; returns the per-mode result.</summary>
    IReadOnlyList<HotkeyRegistration> Activate(HotkeySettings bindings);

    void Deactivate();

    /// <summary>
    /// True when another application currently owns <paramref name="binding"/>. Bindings
    /// held by this service are not reported as conflicts.
    /// </summary>
    bool IsTakenByAnotherApp(HotkeyBinding binding);

    /// <summary>
    /// Register extra keys for a short time (the region overlay's keys, so they work even if
    /// the overlay never gets keyboard focus). Keys another program holds are skipped. The
    /// callback runs off the hotkey thread, one press at a time and in the order the keys were
    /// pressed. Dispose to release the keys.
    /// </summary>
    /// <param name="bindings">The keys to hold.</param>
    /// <param name="callback">Called with the binding of each press.</param>
    /// <param name="repeat">Whether holding a key down repeats it (arrow keys) instead of firing once.</param>
    IDisposable RegisterTemporary(IReadOnlyList<HotkeyBinding> bindings, Action<HotkeyBinding> callback, bool repeat = false);
}
