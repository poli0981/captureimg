// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

namespace CaptureImage.Core.Capture;

/// <summary>Per-request switches for the capture engine.</summary>
/// <param name="PreferHdr">
/// Request a 16-bit floating-point (scRGB) frame. Only honored when the source is on an
/// HDR display; otherwise an 8-bit BGRA frame is returned.
/// </param>
/// <param name="IncludeCursor">Draw the mouse cursor into the frame.</param>
/// <param name="IncludeWindowFrame">
/// Keep the title bar and borders of a window capture. Off by default — games are captured
/// from their client area only.
/// </param>
public sealed record CaptureOptions(
    bool PreferHdr = false,
    bool IncludeCursor = false,
    bool IncludeWindowFrame = false)
{
    public static CaptureOptions Default { get; } = new();
}
