// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Collections.Concurrent;
using System.Text;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;

namespace CaptureImage.Core.Tests.TestSupport;

/// <summary>One <see cref="IImageEncoder.EncodeAsync"/> call as seen by <see cref="FakeImageEncoder"/>.</summary>
/// <param name="Frame">The frame that was encoded.</param>
/// <param name="Format">The requested format.</param>
/// <param name="Settings">The encoder options.</param>
/// <param name="DestinationPath">File the destination stream writes to (the writer's temp file).</param>
internal sealed record EncodeCall(CapturedFrame Frame, ImageFormat Format, EncoderSettings Settings, string? DestinationPath);

/// <summary>
/// Writes a short, format-specific marker instead of a real image. The marker is written in
/// two halves with <see cref="Midway"/> in between, so a failure there leaves a partially
/// written file behind — exactly what the writer has to clean up.
/// </summary>
internal sealed class FakeImageEncoder : IImageEncoder
{
    private readonly ConcurrentQueue<EncodeCall> _calls = new();
    private readonly HashSet<(ImageFormat, FramePixelFormat)> _supported;

    /// <param name="supported">Format/pixel-format pairs to accept; empty means <see cref="DefaultSupport"/>.</param>
    public FakeImageEncoder(params (ImageFormat Format, FramePixelFormat PixelFormat)[] supported)
    {
        _supported = supported.Length > 0 ? [.. supported] : [.. DefaultSupport];
    }

    /// <summary>Every 8-bit output format, plus the JPEG XR sidecar from FP16.</summary>
    public static IReadOnlyList<(ImageFormat Format, FramePixelFormat PixelFormat)> DefaultSupport { get; } =
    [
        (ImageFormat.Png, FramePixelFormat.Bgra8),
        (ImageFormat.Jpeg, FramePixelFormat.Bgra8),
        (ImageFormat.Webp, FramePixelFormat.Bgra8),
        (ImageFormat.Tiff, FramePixelFormat.Bgra8),
        (ImageFormat.Jxr, FramePixelFormat.RgbaHalf),
    ];

    /// <summary>Runs after the first half of the marker is written; throw or wait from here.</summary>
    public Func<EncodeCall, CancellationToken, Task>? Midway { get; set; }

    public IReadOnlyList<EncodeCall> Calls => [.. _calls];

    /// <summary>The exact bytes written for <paramref name="format"/>.</summary>
    public static byte[] ContentFor(ImageFormat format) => Encoding.ASCII.GetBytes($"fake {format} image data");

    public bool Supports(ImageFormat format, FramePixelFormat pixelFormat) => _supported.Contains((format, pixelFormat));

    public async Task EncodeAsync(
        CapturedFrame frame,
        ImageFormat format,
        EncoderSettings settings,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        var call = new EncodeCall(frame, format, settings, (destination as FileStream)?.Name);
        _calls.Enqueue(call);

        var content = ContentFor(format);
        var half = content.Length / 2;
        await destination.WriteAsync(content.AsMemory(0, half), cancellationToken);
        if (Midway is { } midway)
        {
            await midway(call, cancellationToken);
        }

        await destination.WriteAsync(content.AsMemory(half), cancellationToken);
    }
}
