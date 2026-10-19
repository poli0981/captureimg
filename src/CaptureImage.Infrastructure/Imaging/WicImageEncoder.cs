// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Errors;
using CaptureImage.Core.Models;
using Vortice.WIC;

namespace CaptureImage.Infrastructure.Imaging;

/// <summary>
/// TIFF (8-bit, opaque, LZW) and JPEG XR (HDR, 64bpp RGBA half float) through the Windows
/// Imaging Component, which ships with Windows — no third-party codec needed.
/// </summary>
public sealed class WicImageEncoder : IImageEncoder, IDisposable
{
    private readonly Lazy<IWICImagingFactory> _factory = new(() => new IWICImagingFactory(), LazyThreadSafetyMode.ExecutionAndPublication);

    public bool Supports(ImageFormat format, FramePixelFormat pixelFormat) =>
        (format == ImageFormat.Tiff && pixelFormat == FramePixelFormat.Bgra8)
        || (format == ImageFormat.Jxr && pixelFormat == FramePixelFormat.RgbaHalf);

    public Task EncodeAsync(
        CapturedFrame frame,
        ImageFormat format,
        EncoderSettings settings,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(destination);
        if (!Supports(format, frame.Format))
        {
            throw new CaptureException(CaptureError.EncodingFailure, $"WicImageEncoder cannot write {format} from {frame.Format}.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var container = format == ImageFormat.Tiff ? ContainerFormat.Tiff : ContainerFormat.Wmp;
        var pixelFormat = format == ImageFormat.Tiff ? PixelFormat.Format32bppBGRA : PixelFormat.Format64bppRGBAHalf;

        // WIC writes through an IStream wrapper; encode into memory, then copy, so the caller's
        // stream never sees a half-written image if WIC fails midway.
        using var buffer = new MemoryStream();
        lock (_factory)
        {
            using var encoder = _factory.Value.CreateEncoder(container, buffer, BitmapEncoderCacheOption.NoCache);
            using var frameEncode = encoder.CreateNewFrame(out var options);
            using (options)
            {
                frameEncode.Initialize(options);
            }

            frameEncode.SetSize((uint)frame.Width, (uint)frame.Height);
            var requested = pixelFormat;
            frameEncode.SetPixelFormat(ref requested);
            if (requested != pixelFormat)
            {
                throw new CaptureException(CaptureError.EncodingFailure, $"WIC {format} encoder does not accept the frame's pixel format.");
            }

            frameEncode.WritePixels((uint)frame.Height, (uint)frame.RowStride, frame.Pixels).CheckError();
            frameEncode.Commit();
            encoder.Commit();
        }

        buffer.Position = 0;
        buffer.CopyTo(destination);
        destination.Flush();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_factory.IsValueCreated)
        {
            _factory.Value.Dispose();
        }
    }
}
