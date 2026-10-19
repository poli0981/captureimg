// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Runtime.InteropServices;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Errors;
using CaptureImage.Core.Models;
using SkiaSharp;

namespace CaptureImage.Infrastructure.Imaging;

/// <summary>
/// PNG, JPEG and WebP through SkiaSharp. The frame buffer is wrapped in place (row stride
/// honored, no copies) as opaque BGRA and encoded straight into the destination stream.
/// </summary>
public sealed class SkiaImageEncoder : IImageEncoder
{
    public bool Supports(ImageFormat format, FramePixelFormat pixelFormat) =>
        pixelFormat == FramePixelFormat.Bgra8 && format is ImageFormat.Png or ImageFormat.Jpeg or ImageFormat.Webp;

    public Task EncodeAsync(
        CapturedFrame frame,
        ImageFormat format,
        EncoderSettings settings,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(destination);
        if (!Supports(format, frame.Format))
        {
            throw new CaptureException(CaptureError.EncodingFailure, $"SkiaImageEncoder cannot write {format} from {frame.Format}.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var info = new SKImageInfo(frame.Width, frame.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        var handle = GCHandle.Alloc(frame.Pixels, GCHandleType.Pinned);
        try
        {
            using var pixmap = new SKPixmap(info, handle.AddrOfPinnedObject(), frame.RowStride);
            var ok = format switch
            {
                ImageFormat.Png => pixmap.Encode(destination, new SKPngEncoderOptions(SKPngEncoderFilterFlags.AllFilters, ZlibLevel(settings.PngCompression))),
                ImageFormat.Jpeg => pixmap.Encode(destination, new SKJpegEncoderOptions(
                    Quality(settings.JpegQuality),
                    settings.JpegQuality >= 90 ? SKJpegEncoderDownsample.Downsample444 : SKJpegEncoderDownsample.Downsample420,
                    SKJpegEncoderAlphaOption.Ignore)),
                ImageFormat.Webp => pixmap.Encode(destination, new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, Quality(settings.WebpQuality))),
                _ => false,
            };

            if (!ok)
            {
                throw new CaptureException(CaptureError.EncodingFailure, $"SkiaSharp failed to encode {format}.");
            }
        }
        finally
        {
            handle.Free();
        }

        return Task.CompletedTask;
    }

    private static int ZlibLevel(PngCompressionLevel level) => level switch
    {
        PngCompressionLevel.Fast => 1,
        PngCompressionLevel.Smallest => 9,
        _ => 6,
    };

    private static int Quality(int quality) => Math.Clamp(quality, 1, 100);
}
