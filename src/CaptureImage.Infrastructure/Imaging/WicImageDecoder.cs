// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using Microsoft.Extensions.Logging;
using SharpGen.Runtime;
using Vortice.WIC;

namespace CaptureImage.Infrastructure.Imaging;

/// <summary>Decodes PNG / JPEG / WebP / TIFF / JPEG XR files into BGRA frames with WIC.</summary>
public sealed partial class WicImageDecoder : IImageDecoder, IDisposable
{
    private readonly ILogger<WicImageDecoder> _logger;
    private readonly Lazy<IWICImagingFactory> _factory = new(() => new IWICImagingFactory(), LazyThreadSafetyMode.ExecutionAndPublication);

    public WicImageDecoder(ILogger<WicImageDecoder> logger)
    {
        _logger = logger;
    }

    public Task<CapturedFrame?> LoadAsync(string path, int maxDimension = 0, CancellationToken cancellationToken = default) =>
        Task.Run(() => Load(path, maxDimension), cancellationToken);

    public void Dispose()
    {
        if (_factory.IsValueCreated)
        {
            _factory.Value.Dispose();
        }
    }

    private CapturedFrame? Load(string path, int maxDimension)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var factory = _factory.Value;
            using var decoder = factory.CreateDecoderFromFileName(path, FileAccess.Read, DecodeOptions.CacheOnDemand);
            using var frame = decoder.GetFrame(0);
            var size = frame.Size;
            var width = size.Width;
            var height = size.Height;
            if (maxDimension > 0 && Math.Max(width, height) > maxDimension)
            {
                var scale = maxDimension / (double)Math.Max(width, height);
                width = Math.Max(1, (int)Math.Round(width * scale));
                height = Math.Max(1, (int)Math.Round(height * scale));
            }

            using var scaler = factory.CreateBitmapScaler();
            scaler.Initialize(frame, (uint)width, (uint)height, BitmapInterpolationMode.Fant);
            using var converter = factory.CreateFormatConverter();
            converter.Initialize(scaler, PixelFormat.Format32bppBGRA, BitmapDitherType.None, null, 0, BitmapPaletteType.Custom);

            var stride = width * 4;
            var pixels = new byte[stride * height];
            converter.CopyPixels((uint)stride, pixels);
            var result = new CapturedFrame(width, height, stride, pixels);
            Core.Imaging.FrameOperations.ForceOpaque(result);
            return result;
        }
        catch (Exception ex) when (ex is SharpGenException or IOException or UnauthorizedAccessException)
        {
            LogDecodeFailed(ex, Path.GetFileName(path));
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not decode {FileName}.")]
    private partial void LogDecodeFailed(Exception exception, string fileName);
}
