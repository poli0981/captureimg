// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CaptureImage.Core.Imaging;
using CaptureImage.Core.Models;

namespace CaptureImage.UI.Imaging;

/// <summary>Conversions from captured frames and files to WPF bitmaps.</summary>
internal static class FrameBitmap
{
    /// <summary>
    /// A frozen bitmap of an 8-bit frame (HDR frames are tone-mapped first). With
    /// <paramref name="dpi"/> equal to the monitor's DPI, one image pixel covers one physical pixel.
    /// </summary>
    public static BitmapSource ToBitmapSource(CapturedFrame frame, double dpi = 96)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var sdr = frame.IsHdr ? HdrToneMapper.ToSdr(frame) : frame;
        var bitmap = BitmapSource.Create(sdr.Width, sdr.Height, dpi, dpi, PixelFormats.Bgr32, null, sdr.Pixels, sdr.RowStride);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>A frozen copy scaled so its long edge is at most <paramref name="maxDimension"/> pixels.</summary>
    public static BitmapSource Downscale(BitmapSource source, int maxDimension)
    {
        ArgumentNullException.ThrowIfNull(source);
        var longEdge = Math.Max(source.PixelWidth, source.PixelHeight);
        if (longEdge <= maxDimension)
        {
            return source;
        }

        var scale = maxDimension / (double)longEdge;
        var scaled = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        // Copy the pixels so the full-size source can be collected.
        var stride = scaled.PixelWidth * 4;
        var pixels = new byte[stride * scaled.PixelHeight];
        scaled.CopyPixels(pixels, stride, 0);
        var copy = BitmapSource.Create(scaled.PixelWidth, scaled.PixelHeight, 96, 96, PixelFormats.Bgr32, null, pixels, stride);
        copy.Freeze();
        return copy;
    }

    /// <summary>PNG bytes of a frozen bitmap (clipboard "PNG" format keeps full quality).</summary>
    public static MemoryStream EncodePng(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// Decode an image file for display without locking it (thumbnails can be deleted while
    /// shown). Returns <c>null</c> when the file is missing or unreadable.
    /// </summary>
    public static BitmapImage? LoadFile(string? path, int decodePixelWidth = 0)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            if (decodePixelWidth > 0)
            {
                image.DecodePixelWidth = decodePixelWidth;
            }
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Decode PNG bytes (window icons) for display.</summary>
    public static BitmapImage? LoadBytes(byte[]? bytes)
    {
        if (bytes is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException or InvalidOperationException)
        {
            return null;
        }
    }
}
