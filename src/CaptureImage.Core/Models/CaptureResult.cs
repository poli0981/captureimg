// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Capture;
using CaptureImage.Core.Errors;
using CaptureImage.Core.Library;

namespace CaptureImage.Core.Models;

/// <summary>How a capture request ended.</summary>
public enum CaptureOutcomeKind
{
    /// <summary>Saved to disk (and possibly copied).</summary>
    Saved,

    /// <summary>Copied to the clipboard only (clipboard mode "Copy").</summary>
    Copied,

    /// <summary>The user cancelled the countdown or the region selection.</summary>
    Cancelled,

    /// <summary>The user discarded the capture in the preview.</summary>
    Discarded,

    /// <summary>Another capture was still running; the request was ignored.</summary>
    Busy,

    /// <summary>The capture or the save failed.</summary>
    Failed,
}

/// <summary>Result of <c>CaptureCoordinator.CaptureAsync</c>.</summary>
public sealed record CaptureOutcome(CaptureOutcomeKind Kind, CaptureMode Mode)
{
    /// <summary>The saved capture (for <see cref="CaptureOutcomeKind.Saved"/>).</summary>
    public CaptureRecord? Record { get; init; }

    public CaptureError? Error { get; init; }

    public string? ErrorMessage { get; init; }

    public TimeSpan Duration { get; init; }

    public bool IsSuccess => Kind is CaptureOutcomeKind.Saved or CaptureOutcomeKind.Copied;
}
