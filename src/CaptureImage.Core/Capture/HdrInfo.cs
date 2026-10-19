// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

namespace CaptureImage.Core.Capture;

/// <summary>
/// HDR state of a display. Luminance values are in nits. When Windows reports nothing
/// usable the defaults describe a plain SDR monitor (80-nit reference white).
/// </summary>
/// <param name="IsHdrActive">The output currently runs in an HDR (PQ / BT.2100) color space.</param>
/// <param name="SdrWhiteNits">Brightness Windows maps SDR white to ("SDR content brightness").</param>
/// <param name="MaxLuminanceNits">Peak luminance reported by the display.</param>
public sealed record HdrInfo(bool IsHdrActive, float SdrWhiteNits, float MaxLuminanceNits)
{
    /// <summary>scRGB defines 1.0 as 80 nits.</summary>
    public const float ScRgbReferenceWhiteNits = 80f;

    public static HdrInfo Sdr { get; } = new(false, ScRgbReferenceWhiteNits, ScRgbReferenceWhiteNits);
}
