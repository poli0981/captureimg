// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Models;

namespace CaptureImage.Core.Abstractions;

/// <summary>
/// Event-driven view of the desktop's top-level windows (Win32 WinEvent hooks — no polling).
/// Events are raised on a background thread.
/// </summary>
public interface IWindowWatcher : IDisposable
{
    /// <summary>Top-level windows appeared, disappeared or were renamed (debounced).</summary>
    event EventHandler? WindowsChanged;

    /// <summary>A different window became the foreground window.</summary>
    event EventHandler<nint>? ForegroundChanged;

    bool IsRunning { get; }

    void Start();

    void Stop();
}

/// <summary>Builds <see cref="GameTarget"/>s for capturable top-level windows.</summary>
public interface ITargetCatalog
{
    /// <summary>All visible, capturable top-level windows of other processes.</summary>
    Task<IReadOnlyList<GameTarget>> EnumerateAsync(CancellationToken cancellationToken = default);

    /// <summary>Describe one window, or <c>null</c> when it is gone or not capturable.</summary>
    Task<GameTarget?> GetTargetAsync(nint windowHandle, CancellationToken cancellationToken = default);

    /// <summary>The current foreground window handle (0 when none).</summary>
    nint GetForegroundWindow();
}
