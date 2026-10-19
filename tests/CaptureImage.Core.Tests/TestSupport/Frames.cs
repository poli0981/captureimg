// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Buffers.Binary;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;

namespace CaptureImage.Core.Tests.TestSupport;

/// <summary>One 8-bit pixel in memory order.</summary>
internal readonly record struct BgraPixel(byte B, byte G, byte R, byte A);

/// <summary>One FP16 pixel (linear scRGB) as floats.</summary>
internal readonly record struct RgbaPixel(float R, float G, float B, float A);

/// <summary>
/// Builds <see cref="CapturedFrame"/>s by hand. Optional row padding imitates GPU readback
/// pitch; padding bytes hold values that would be conspicuous if an operation read them.
/// </summary>
internal static class Frames
{
    /// <summary>Filler for the padding of 8-bit rows.</summary>
    public const byte PaddingByte = 0xEE;

    /// <summary>Filler for the padding of FP16 rows: the largest finite half (65504).</summary>
    public static readonly Half PaddingHalf = Half.MaxValue;

    public static CapturedFrame CreateBgra(int width, int height, Func<int, int, BgraPixel> pixel, int paddingBytes = 0)
    {
        var stride = (width * 4) + paddingBytes;
        var pixels = new byte[stride * height];
        Array.Fill(pixels, PaddingByte);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var p = pixel(x, y);
                var offset = (y * stride) + (x * 4);
                pixels[offset] = p.B;
                pixels[offset + 1] = p.G;
                pixels[offset + 2] = p.R;
                pixels[offset + 3] = p.A;
            }
        }

        return new CapturedFrame(width, height, stride, pixels, FramePixelFormat.Bgra8);
    }

    public static CapturedFrame SolidBgra(int width, int height, BgraPixel color, int paddingBytes = 0) =>
        CreateBgra(width, height, (_, _) => color, paddingBytes);

    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="pixel">Pixel value for (x, y).</param>
    /// <param name="paddingPixels">Extra pixels' worth of padding at the end of every row.</param>
    /// <param name="hdr">Display state attached to the frame.</param>
    public static CapturedFrame CreateHalf(
        int width,
        int height,
        Func<int, int, RgbaPixel> pixel,
        int paddingPixels = 0,
        HdrInfo? hdr = null)
    {
        var stride = (width + paddingPixels) * 8;
        var pixels = new byte[stride * height];
        for (var i = 0; i < pixels.Length; i += 2)
        {
            BinaryPrimitives.WriteHalfLittleEndian(pixels.AsSpan(i), PaddingHalf);
        }

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var p = pixel(x, y);
                var span = pixels.AsSpan((y * stride) + (x * 8), 8);
                BinaryPrimitives.WriteHalfLittleEndian(span, (Half)p.R);
                BinaryPrimitives.WriteHalfLittleEndian(span[2..], (Half)p.G);
                BinaryPrimitives.WriteHalfLittleEndian(span[4..], (Half)p.B);
                BinaryPrimitives.WriteHalfLittleEndian(span[6..], (Half)p.A);
            }
        }

        return new CapturedFrame(width, height, stride, pixels, FramePixelFormat.RgbaHalf) { Hdr = hdr };
    }

    public static CapturedFrame SolidHalf(int width, int height, RgbaPixel color, HdrInfo? hdr = null) =>
        CreateHalf(width, height, (_, _) => color, hdr: hdr);

    public static BgraPixel ReadBgra(CapturedFrame frame, int x, int y)
    {
        var offset = (y * frame.RowStride) + (x * 4);
        var p = frame.Pixels;
        return new BgraPixel(p[offset], p[offset + 1], p[offset + 2], p[offset + 3]);
    }

    public static RgbaPixel ReadHalf(CapturedFrame frame, int x, int y)
    {
        var span = frame.Pixels.AsSpan((y * frame.RowStride) + (x * 8), 8);
        return new RgbaPixel(
            (float)BinaryPrimitives.ReadHalfLittleEndian(span),
            (float)BinaryPrimitives.ReadHalfLittleEndian(span[2..]),
            (float)BinaryPrimitives.ReadHalfLittleEndian(span[4..]),
            (float)BinaryPrimitives.ReadHalfLittleEndian(span[6..]));
    }

    /// <summary>The bytes after the last pixel of row <paramref name="y"/>.</summary>
    public static byte[] RowPadding(CapturedFrame frame, int y) =>
        frame.Pixels.AsSpan((y * frame.RowStride) + frame.TightStride, frame.RowStride - frame.TightStride).ToArray();
}
