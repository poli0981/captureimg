// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

namespace CaptureImage.Core.Capture;

/// <summary>A display attached to the desktop, in physical pixels.</summary>
/// <param name="Handle">Win32 <c>HMONITOR</c>.</param>
/// <param name="DeviceName">GDI device name, e.g. <c>\.\DISPLAY1</c>.</param>
/// <param name="Bounds">Monitor rectangle in virtual-desktop coordinates.</param>
/// <param name="WorkArea">Bounds minus the taskbar and docked app bars.</param>
/// <param name="Dpi">Effective DPI (96 = 100% scaling).</param>
/// <param name="IsPrimary">Whether this is the primary monitor.</param>
/// <param name="Hdr">HDR state of the output.</param>
public sealed record MonitorInfo(
    nint Handle,
    string DeviceName,
    PixelRect Bounds,
    PixelRect WorkArea,
    uint Dpi,
    bool IsPrimary,
    HdrInfo Hdr)
{
    /// <summary>Scale factor relative to 96 DPI (1.0 = 100%).</summary>
    public double Scale => Dpi / 96.0;
}
