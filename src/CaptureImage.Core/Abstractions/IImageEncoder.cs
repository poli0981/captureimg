// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Models;

namespace CaptureImage.Core.Abstractions;

/// <summary>PNG zlib effort.</summary>
public enum PngCompressionLevel
{
    /// <summary>Fastest save, larger files.</summary>
    Fast,

    /// <summary>Default trade-off.</summary>
    Balanced,

    /// <summary>Smallest files, slowest save.</summary>
    Smallest,
}

/// <summary>Text metadata embedded into saved images where the format supports it.</summary>
public sealed record ImageMetadata(string? Title, string Software, DateTimeOffset CapturedAt);

/// <summary>Per-save encoder options.</summary>
public sealed record EncoderSettings(
    int JpegQuality = 90,
    int WebpQuality = 85,
    PngCompressionLevel PngCompression = PngCompressionLevel.Balanced,
    ImageMetadata? Metadata = null);

/// <summary>Encodes a <see cref="CapturedFrame"/> into an image file stream.</summary>
public interface IImageEncoder
{
    /// <summary>True if this encoder can write <paramref name="format"/> from <paramref name="pixelFormat"/> input.</summary>
    bool Supports(ImageFormat format, FramePixelFormat pixelFormat);

    /// <summary>Encode into <paramref name="destination"/> (flushed, not closed).</summary>
    Task EncodeAsync(
        CapturedFrame frame,
        ImageFormat format,
        EncoderSettings settings,
        Stream destination,
        CancellationToken cancellationToken = default);
}
