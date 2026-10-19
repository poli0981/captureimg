// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;

namespace CaptureImage.Core.Imaging;

/// <summary>Pure CPU operations on <see cref="CapturedFrame"/>s.</summary>
public static class FrameOperations
{
    /// <summary>
    /// Copy the <paramref name="area"/> of <paramref name="frame"/> (frame-relative pixels) into a
    /// new tightly packed frame. The area is clipped to the frame first.
    /// </summary>
    /// <exception cref="ArgumentException">The area does not overlap the frame.</exception>
    public static CapturedFrame Crop(CapturedFrame frame, PixelRect area)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var clipped = area.Intersect(new PixelRect(0, 0, frame.Width, frame.Height));
        if (clipped.IsEmpty)
        {
            throw new ArgumentException($"Crop area {area} lies outside the {frame.Width}x{frame.Height} frame.", nameof(area));
        }

        var bpp = frame.BytesPerPixel;
        var rowBytes = clipped.Width * bpp;
        var pixels = new byte[rowBytes * clipped.Height];
        for (var row = 0; row < clipped.Height; row++)
        {
            var source = ((clipped.Y + row) * frame.RowStride) + (clipped.X * bpp);
            Buffer.BlockCopy(frame.Pixels, source, pixels, row * rowBytes, rowBytes);
        }

        return new CapturedFrame(clipped.Width, clipped.Height, rowBytes, pixels, frame.Format) { Hdr = frame.Hdr };
    }

    /// <summary>
    /// Set every alpha byte of an 8-bit frame to 255, in place. Window captures can carry
    /// partially transparent pixels (Mica, rounded corners, GDI sources); saved screenshots
    /// must never be see-through.
    /// </summary>
    public static void ForceOpaque(CapturedFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Format != FramePixelFormat.Bgra8)
        {
            return;
        }

        var pixels = frame.Pixels;
        for (var y = 0; y < frame.Height; y++)
        {
            var offset = (y * frame.RowStride) + 3;
            var end = offset + (frame.Width * 4);
            for (var i = offset; i < end; i += 4)
            {
                pixels[i] = 0xFF;
            }
        }
    }
}
