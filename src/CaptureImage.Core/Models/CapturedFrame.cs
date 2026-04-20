namespace CaptureImage.Core.Models;

/// <summary>
/// A raw captured frame in BGRA8 pre-multiplied format, ready to hand to an encoder.
/// </summary>
/// <remarks>
/// <para>
/// The pixel buffer is <b>owned</b> by this record — do not mutate it after construction and
/// do not hold on to the underlying <see cref="byte"/> array after the frame has been encoded.
/// The capture engine disposes D3D resources as soon as it hands the frame to the caller,
/// so this is the authoritative copy.
/// </para>
/// <para>
/// Rows may be padded: <see cref="RowStride"/> is the byte distance between the start of
/// consecutive rows, and may be larger than <c>Width * 4</c>. Encoders must respect it.
/// </para>
/// </remarks>
/// <param name="Width">Frame width in pixels.</param>
/// <param name="Height">Frame height in pixels.</param>
/// <param name="RowStride">Byte offset between consecutive rows. Always ≥ <c>Width * 4</c>.</param>
/// <param name="BgraPixels">
/// Raw pixel buffer. Length is always <c>RowStride * Height</c>. Pixel layout is BGRA
/// pre-multiplied (little-endian: byte 0 = B, byte 1 = G, byte 2 = R, byte 3 = A).
/// </param>
public sealed record CapturedFrame(
    int Width,
    int Height,
    int RowStride,
    byte[] BgraPixels)
{
    /// <summary>Total bytes in the pixel buffer.</summary>
    public int SizeBytes => BgraPixels.Length;

    /// <summary>True if the row pitch equals the tight width pitch.</summary>
    public bool IsTightlyPacked => RowStride == Width * 4;

    /// <summary>
    /// Return a heap copy of the pixel buffer with any row padding removed.
    /// Use when you need <c>Width*Height*4</c> bytes exactly — e.g. feeding ImageSharp.
    /// </summary>
    public byte[] ToTightlyPacked()
    {
        if (IsTightlyPacked)
        {
            return BgraPixels;
        }

        var tight = new byte[Width * Height * 4];
        var rowLen = Width * 4;
        for (var y = 0; y < Height; y++)
        {
            Buffer.BlockCopy(BgraPixels, y * RowStride, tight, y * rowLen, rowLen);
        }
        return tight;
    }
}
