// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Runtime.InteropServices;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;

namespace CaptureImage.Core.Imaging;

/// <summary>
/// Converts an HDR capture (linear scRGB, 16-bit float) into an 8-bit sRGB image that looks
/// like what the user saw: SDR content keeps its brightness, highlights above SDR white roll
/// off smoothly instead of clipping.
/// </summary>
/// <remarks>
/// <list type="number">
///   <item>Rescale so the display's "SDR content brightness" maps to 1.0.</item>
///   <item>Clip negative (out-of-sRGB-gamut) components to 0.</item>
///   <item>Above <see cref="KneeStart"/>, compress the max RGB component with an exponential
///   shoulder that is C¹-continuous at the knee and approaches 1.0; scale R, G and B by the same
///   factor so hues are preserved.</item>
///   <item>Encode with the sRGB transfer function through a lookup table.</item>
/// </list>
/// </remarks>
public static class HdrToneMapper
{
    /// <summary>Highlights start rolling off at 80% of SDR white.</summary>
    public const float KneeStart = 0.8f;

    private const int LutSize = 4096;
    private static readonly byte[] SrgbLut = BuildSrgbLut();

    /// <summary>Return an 8-bit BGRA rendition of <paramref name="frame"/>; SDR frames are returned unchanged.</summary>
    public static CapturedFrame ToSdr(CapturedFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Format != FramePixelFormat.RgbaHalf)
        {
            return frame;
        }

        var hdr = frame.Hdr ?? HdrInfo.Sdr;
        var sdrWhite = hdr.SdrWhiteNits > 1f ? hdr.SdrWhiteNits : HdrInfo.ScRgbReferenceWhiteNits;
        var scale = HdrInfo.ScRgbReferenceWhiteNits / sdrWhite;

        var width = frame.Width;
        var outStride = width * 4;
        var output = new byte[outStride * frame.Height];
        var source = frame.Pixels;
        var sourceStride = frame.RowStride;

        Parallel.For(0, frame.Height, y =>
        {
            var row = MemoryMarshal.Cast<byte, Half>(source.AsSpan(y * sourceStride, width * 8));
            var dst = output.AsSpan(y * outStride, outStride);
            for (var x = 0; x < width; x++)
            {
                var r = Math.Max(0f, (float)row[x * 4] * scale);
                var g = Math.Max(0f, (float)row[(x * 4) + 1] * scale);
                var b = Math.Max(0f, (float)row[(x * 4) + 2] * scale);

                var max = Math.Max(r, Math.Max(g, b));
                if (max > KneeStart)
                {
                    var factor = CompressHighlight(max) / max;
                    r *= factor;
                    g *= factor;
                    b *= factor;
                }

                dst[x * 4] = Encode(b);
                dst[(x * 4) + 1] = Encode(g);
                dst[(x * 4) + 2] = Encode(r);
                dst[(x * 4) + 3] = 0xFF;
            }
        });

        return new CapturedFrame(width, frame.Height, outStride, output, FramePixelFormat.Bgra8);
    }

    /// <summary>The shoulder curve applied to the max RGB component (exposed for tests).</summary>
    public static float CompressHighlight(float value)
    {
        if (value <= KneeStart)
        {
            return value;
        }

        const float range = 1f - KneeStart;
        return KneeStart + (range * (1f - MathF.Exp(-(value - KneeStart) / range)));
    }

    /// <summary>sRGB OETF for a linear value in [0, 1] (exposed for tests).</summary>
    public static float LinearToSrgb(float linear) =>
        linear <= 0.0031308f ? 12.92f * linear : (1.055f * MathF.Pow(linear, 1f / 2.4f)) - 0.055f;

    private static byte Encode(float linear)
    {
        var index = (int)((linear * (LutSize - 1)) + 0.5f);
        return SrgbLut[Math.Clamp(index, 0, LutSize - 1)];
    }

    private static byte[] BuildSrgbLut()
    {
        var lut = new byte[LutSize];
        for (var i = 0; i < LutSize; i++)
        {
            var encoded = LinearToSrgb(i / (float)(LutSize - 1));
            lut[i] = (byte)Math.Clamp((int)((encoded * 255f) + 0.5f), 0, 255);
        }
        return lut;
    }
}
