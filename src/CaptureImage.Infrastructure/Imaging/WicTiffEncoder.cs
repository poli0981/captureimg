// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Runtime.Versioning;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Errors;
using CaptureImage.Core.Models;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace CaptureImage.Infrastructure.Imaging;

/// <summary>
/// TIFF encoder backed by the Windows Imaging Component (via <see cref="BitmapEncoder"/>).
/// SkiaSharp cannot emit TIFF; WIC ships with Windows, so this needs no extra dependency.
/// Captures are written opaque (alpha ignored) with lossless LZW compression.
/// </summary>
[SupportedOSPlatform("windows10.0.17763.0")]
public sealed class WicTiffEncoder : IImageEncoder
{
    // WIC "TiffCompressionMethod" encoder option: 4 = WICTiffCompressionLZW.
    private const byte TiffCompressionLzw = 4;

    public bool Supports(ImageFormat format) => format == ImageFormat.Tiff;

    public async Task EncodeAsync(
        CapturedFrame frame,
        ImageFormat format,
        int jpegQuality,
        int webpQuality,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        if (format != ImageFormat.Tiff)
        {
            throw new CaptureException(CaptureError.EncodingFailure,
                $"WicTiffEncoder does not support {format}.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // BitmapEncoder.SetPixelData expects tightly packed rows (stride == width * 4).
        var pixels = frame.IsTightlyPacked ? frame.BgraPixels : frame.ToTightlyPacked();

        using var buffer = new InMemoryRandomAccessStream();
        var options = new BitmapPropertySet
        {
            { "TiffCompressionMethod", new BitmapTypedValue(TiffCompressionLzw, Windows.Foundation.PropertyType.UInt8) },
        };

        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.TiffEncoderId, buffer, options)
            .AsTask(cancellationToken).ConfigureAwait(false);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Ignore,
            (uint)frame.Width,
            (uint)frame.Height,
            dpiX: 96,
            dpiY: 96,
            pixels);
        await encoder.FlushAsync().AsTask(cancellationToken).ConfigureAwait(false);

        buffer.Seek(0);
        await using var source = buffer.AsStreamForRead();
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
