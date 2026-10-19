// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;

namespace CaptureImage.Core.Abstractions;

/// <summary>Grabs a single frame from a window or a monitor.</summary>
public interface ICaptureEngine
{
    /// <summary>
    /// Capture one frame. 8-bit frames always come back opaque. Failures are reported as
    /// <see cref="Errors.CaptureException"/> with a specific <see cref="Errors.CaptureError"/>.
    /// </summary>
    Task<CapturedFrame> CaptureAsync(CaptureSource source, CaptureOptions options, CancellationToken cancellationToken = default);
}

/// <summary>Monitor layout, DPI and HDR state.</summary>
public interface IDisplayService
{
    IReadOnlyList<MonitorInfo> GetMonitors();

    MonitorInfo? GetMonitorForWindow(nint windowHandle);

    MonitorInfo? GetMonitorAtCursor();
}
