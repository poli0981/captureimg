// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

namespace CaptureImage.Core.Capture;

/// <summary>What a capture request grabs. Each mode has its own global hotkey.</summary>
public enum CaptureMode
{
    /// <summary>The locked target window, or the foreground window when nothing is locked.</summary>
    Window,

    /// <summary>A rectangle the user drags on a frozen snapshot of one monitor.</summary>
    Region,

    /// <summary>The whole monitor under the mouse cursor.</summary>
    Monitor,
}
