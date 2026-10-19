// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Buffers.Binary;
using AwesomeAssertions;
using CaptureImage.Core.Models;

namespace CaptureImage.Infrastructure.Tests.TestSupport;

/// <summary>An 8-bit sRGB colour.</summary>
internal readonly record struct Rgb(byte R, byte G, byte B);

/// <summary>
/// Synthetic frames for encoder/decoder tests, plus pixel accessors. Row padding is filled
/// with <see cref="PaddingByte"/> so a codec that ignores <see cref="CapturedFrame.RowStride"/>
/// produces visibly wrong pixels instead of passing by accident.
/// </summary>
internal static class TestFrames
{
    private const byte PaddingByte = 0xCD;

    // Channel-asymmetric colours: an R/B swap or a premultiplication shows up immediately.
    public static readonly Rgb Red = new(220, 30, 40);
    private static readonly Rgb Green = new(30, 200, 60);
    private static readonly Rgb Blue = new(40, 50, 210);
    private static readonly Rgb Yellow = new(230, 200, 20);

    /// <summary>A BGRA frame; <paramref name="padding"/> extra bytes are added to every row.</summary>
    public static CapturedFrame Bgra(int width, int height, Func<int, int, Rgb> color, int padding = 0, byte alpha = 0xFF)
    {
        var stride = (width * 4) + padding;
        var pixels = new byte[stride * height];
        Array.Fill(pixels, PaddingByte);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var c = color(x, y);
                var i = (y * stride) + (x * 4);
                pixels[i] = c.B;
                pixels[i + 1] = c.G;
                pixels[i + 2] = c.R;
                pixels[i + 3] = alpha;
            }
        }
        return new CapturedFrame(width, height, stride, pixels);
    }

    /// <summary>A linear scRGB half-float frame (alpha 1.0), as an HDR capture produces.</summary>
    public static CapturedFrame RgbaHalf(int width, int height, Func<int, int, (float R, float G, float B)> color, int padding = 0)
    {
        var stride = (width * 8) + padding;
        var pixels = new byte[stride * height];
        Array.Fill(pixels, PaddingByte);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var (r, g, b) = color(x, y);
                var pixel = pixels.AsSpan((y * stride) + (x * 8), 8);
                BinaryPrimitives.WriteHalfLittleEndian(pixel, (Half)r);
                BinaryPrimitives.WriteHalfLittleEndian(pixel[2..], (Half)g);
                BinaryPrimitives.WriteHalfLittleEndian(pixel[4..], (Half)b);
                BinaryPrimitives.WriteHalfLittleEndian(pixel[6..], Half.One);
            }
        }
        return new CapturedFrame(width, height, stride, pixels, FramePixelFormat.RgbaHalf);
    }

    /// <summary>A different colour for (almost) every pixel — for lossless formats.</summary>
    public static Rgb Gradient(int x, int y) =>
        new((byte)(x * 23 % 256), (byte)(y * 41 % 256), (byte)((x + y) * 13 % 256));

    /// <summary>Four flat quadrants — for lossy formats, compare at <see cref="QuadrantCenters"/>.</summary>
    public static Func<int, int, Rgb> Quadrants(int width, int height) =>
        (x, y) => (x < width / 2, y < height / 2) switch
        {
            (true, true) => Red,
            (false, true) => Green,
            (true, false) => Blue,
            _ => Yellow,
        };

    public static IReadOnlyList<(int X, int Y, Rgb Color)> QuadrantCenters(int width, int height) =>
    [
        (width / 4, height / 4, Red),
        (width * 3 / 4, height / 4, Green),
        (width / 4, height * 3 / 4, Blue),
        (width * 3 / 4, height * 3 / 4, Yellow),
    ];

    public static Rgb PixelAt(CapturedFrame frame, int x, int y)
    {
        var i = (y * frame.RowStride) + (x * 4);
        return new Rgb(frame.Pixels[i + 2], frame.Pixels[i + 1], frame.Pixels[i]);
    }

    public static bool IsOpaque(CapturedFrame frame)
    {
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                if (frame.Pixels[(y * frame.RowStride) + (x * 4) + 3] != 0xFF)
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>Same size and identical RGB in every pixel (8-bit frames).</summary>
    public static void ShouldHaveSamePixelsAs(this CapturedFrame actual, CapturedFrame expected)
    {
        actual.Width.Should().Be(expected.Width);
        actual.Height.Should().Be(expected.Height);
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                PixelAt(actual, x, y).Should().Be(PixelAt(expected, x, y), "pixel ({0}, {1}) must survive the round trip", x, y);
            }
        }
    }

    /// <summary>Every channel within <paramref name="tolerance"/> of the expected value.</summary>
    public static void ShouldBeCloseTo(this Rgb actual, Rgb expected, int tolerance, string because = "")
    {
        var distance = Math.Max(Math.Abs(actual.R - expected.R), Math.Max(Math.Abs(actual.G - expected.G), Math.Abs(actual.B - expected.B)));
        distance.Should().BeLessThanOrEqualTo(tolerance, "{0} should be within {1} of {2} {3}", actual, tolerance, expected, because);
    }
}
