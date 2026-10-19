// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

namespace CaptureImage.Core.Errors;

/// <summary>Why a capture failed. Each value maps to a localized user message.</summary>
public enum CaptureError
{
    /// <summary>Unexpected failure; details are in the log.</summary>
    Unknown = 0,

    /// <summary>The window or monitor no longer exists.</summary>
    TargetGone = 1,

    /// <summary>The target's process exited during the capture.</summary>
    ProcessExited = 2,

    /// <summary>The content is protected (DRM / capture-excluded window).</summary>
    ProtectedContent = 3,

    /// <summary>Windows.Graphics.Capture produced no frame in time.</summary>
    NoFrameArrived = 4,

    /// <summary>The GPU device failed or was reset.</summary>
    GraphicsDeviceFailure = 5,

    EncodingFailure = 6,

    /// <summary>The image could not be written (permissions, disk full, invalid folder).</summary>
    FileWriteFailure = 7,

    Cancelled = 8,

    /// <summary>The window is minimized; Windows renders no frames for it.</summary>
    TargetMinimized = 10,

    /// <summary>No window was locked and nothing capturable is in the foreground.</summary>
    NoTarget = 11,

    /// <summary>Windows.Graphics.Capture is unavailable on this system.</summary>
    NotSupported = 12,
}
