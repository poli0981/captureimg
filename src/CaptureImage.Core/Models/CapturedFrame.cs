// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Capture;

namespace CaptureImage.Core.Models;

/// <summary>Pixel layout of a <see cref="CapturedFrame"/>.</summary>
public enum FramePixelFormat
{
    /// <summary>8-bit B, G, R, A (alpha forced opaque by the capture engine).</summary>
    Bgra8,

    /// <summary>16-bit float R, G, B, A in linear scRGB (1.0 = 80 nits). HDR captures only.</summary>
    RgbaHalf,
}

/// <summary>
/// A captured image held in CPU memory. Rows may be padded (<see cref="RowStride"/> can be
/// larger than <c>Width × BytesPerPixel</c>) because GPU readback keeps the driver's pitch.
/// </summary>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="RowStride">Bytes between the starts of two consecutive rows.</param>
/// <param name="Pixels">Raw pixel bytes, <see cref="RowStride"/> × <see cref="Height"/> long.</param>
/// <param name="Format">Pixel layout.</param>
public sealed record CapturedFrame(
    int Width,
    int Height,
    int RowStride,
    byte[] Pixels,
    FramePixelFormat Format = FramePixelFormat.Bgra8)
{
    /// <summary>Display HDR state the frame was captured under (set for HDR frames).</summary>
    public HdrInfo? Hdr { get; init; }

    public int BytesPerPixel => Format == FramePixelFormat.Bgra8 ? 4 : 8;

    public bool IsHdr => Format == FramePixelFormat.RgbaHalf;

    /// <summary>Row length without padding.</summary>
    public int TightStride => Width * BytesPerPixel;

    public bool IsTightlyPacked => RowStride == TightStride;

    /// <summary>
    /// The pixels without row padding. Returns <see cref="Pixels"/> itself when already tight,
    /// otherwise a copy — use when an API needs exactly <c>Width × Height × BytesPerPixel</c> bytes.
    /// </summary>
    public byte[] ToTightlyPacked()
    {
        if (IsTightlyPacked && Pixels.Length == TightStride * Height)
        {
            return Pixels;
        }

        var tight = new byte[TightStride * Height];
        for (var y = 0; y < Height; y++)
        {
            Buffer.BlockCopy(Pixels, y * RowStride, tight, y * TightStride, TightStride);
        }
        return tight;
    }
}
