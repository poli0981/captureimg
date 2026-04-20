using System.Runtime.InteropServices;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Errors;
using CaptureImage.Core.Models;
using SkiaSharp;

namespace CaptureImage.Infrastructure.Imaging;

/// <summary>
/// Default <see cref="IImageEncoder"/> for PNG, JPEG, and WebP. Uses SkiaSharp — bundled with
/// Avalonia anyway, hardware-friendly, and handles BGRA pre-multiplied input natively.
/// </summary>
/// <remarks>
/// TIFF is intentionally NOT supported here; use <see cref="ImageSharpTiffEncoder"/> for TIFF.
/// The orchestrator picks the right encoder based on <see cref="Supports(ImageFormat)"/>.
/// </remarks>
public sealed class SkiaImageEncoder : IImageEncoder
{
    public bool Supports(ImageFormat format) =>
        format is ImageFormat.Png or ImageFormat.Jpeg or ImageFormat.Webp;

    public Task EncodeAsync(
        CapturedFrame frame,
        ImageFormat format,
        int jpegQuality,
        int webpQuality,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(format))
        {
            throw new CaptureException(CaptureError.EncodingFailure,
                $"SkiaImageEncoder does not support {format}.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Build an SKBitmap that WRAPS the captured pixel buffer — no extra copy. SkiaSharp
        // needs a contiguous (width*4 == rowBytes) buffer, so if we got padded rows from D3D
        // we feed it a tightly-packed copy.
        var pixels = frame.IsTightlyPacked ? frame.BgraPixels : frame.ToTightlyPacked();
        var rowBytes = frame.Width * 4;

        var info = new SKImageInfo(
            width: frame.Width,
            height: frame.Height,
            colorType: SKColorType.Bgra8888,
            alphaType: SKAlphaType.Premul);

        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            using var bitmap = new SKBitmap();
            if (!bitmap.InstallPixels(info, handle.AddrOfPinnedObject(), rowBytes))
            {
                throw new CaptureException(CaptureError.EncodingFailure,
                    "SkiaSharp InstallPixels rejected the BGRA buffer.");
            }

            using var image = SKImage.FromBitmap(bitmap);
            if (image is null)
            {
                throw new CaptureException(CaptureError.EncodingFailure,
                    "SKImage.FromBitmap returned null.");
            }

            var (skFormat, quality) = format switch
            {
                ImageFormat.Png  => (SKEncodedImageFormat.Png,  100),
                ImageFormat.Jpeg => (SKEncodedImageFormat.Jpeg, ClampQuality(jpegQuality)),
                ImageFormat.Webp => (SKEncodedImageFormat.Webp, ClampQuality(webpQuality)),
                _ => throw new InvalidOperationException("unreachable"),
            };

            using var encoded = image.Encode(skFormat, quality);
            if (encoded is null)
            {
                throw new CaptureException(CaptureError.EncodingFailure,
                    $"SKImage.Encode returned null for {format}.");
            }
            encoded.SaveTo(destination);
        }
        finally
        {
            handle.Free();
        }

        return Task.CompletedTask;
    }

    private static int ClampQuality(int quality)
    {
        if (quality < 1) return 1;
        if (quality > 100) return 100;
        return quality;
    }
}
