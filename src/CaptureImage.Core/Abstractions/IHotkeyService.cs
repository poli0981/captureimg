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
    /// Register extra keys for a short time (Esc / Enter while the region overlay is up, so
    /// they work even if the overlay never gets keyboard focus). Dispose to release them.
    /// </summary>
    IDisposable RegisterTemporary(IReadOnlyList<HotkeyBinding> bindings, Action<HotkeyBinding> callback);
}
