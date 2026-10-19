// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

namespace CaptureImage.Core.Capture;

/// <summary>What the capture engine should read pixels from.</summary>
public abstract record CaptureSource
{
    private protected CaptureSource()
    {
    }
}

/// <summary>A top-level window (<c>HWND</c>).</summary>
public sealed record WindowCaptureSource(nint WindowHandle) : CaptureSource;

/// <summary>A whole monitor (<c>HMONITOR</c>).</summary>
public sealed record MonitorCaptureSource(nint MonitorHandle) : CaptureSource;
