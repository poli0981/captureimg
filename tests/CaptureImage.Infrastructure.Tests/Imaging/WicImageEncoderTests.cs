// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Errors;
using CaptureImage.Core.Imaging;
using CaptureImage.Core.Models;
using CaptureImage.Infrastructure.Imaging;
using CaptureImage.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Imaging;

/// <summary>
/// <see cref="WicImageEncoder"/>: 8-bit TIFF (lossless) and the JPEG XR HDR sidecar from
/// half-float frames, decoded back with <see cref="WicImageDecoder"/>.
/// </summary>
public sealed class WicImageEncoderTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly WicImageDecoder _decoder = new(NullLogger<WicImageDecoder>.Instance);
    private readonly WicImageEncoder _encoder = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _encoder.Dispose();
        _decoder.Dispose();
        _temp.Dispose();
    }

    public static TheoryData<ImageFormat, FramePixelFormat, bool> SupportMatrix()
    {
        HashSet<(ImageFormat, FramePixelFormat)> supported =
        [
            (ImageFormat.Tiff, FramePixelFormat.Bgra8),
            (ImageFormat.Jxr, FramePixelFormat.RgbaHalf),
        ];
        var data = new TheoryData<ImageFormat, FramePixelFormat, bool>();
        foreach (var format in Enum.GetValues<ImageFormat>())
        {
            foreach (var pixelFormat in Enum.GetValues<FramePixelFormat>())
            {
                data.Add(format, pixelFormat, supported.Contains((format, pixelFormat)));
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(SupportMatrix))]
    public void Supports_EightBitTiffAndHalfFloatJpegXr(ImageFormat format, FramePixelFormat pixelFormat, bool expected)
    {
        _encoder.Supports(format, pixelFormat).Should().Be(expected);
    }

    [Fact]
    public async Task Tiff_RoundTripsEveryPixelExactly_FromPaddedRows()
    {
        var frame = TestFrames.Bgra(7, 5, TestFrames.Gradient, padding: 12);

        var path = await EncodeToFileAsync(frame, ImageFormat.Tiff);

        (await File.ReadAllBytesAsync(path, Ct))[..4].Should().Equal(0x49, 0x49, 0x2A, 0x00); // "II*\0", little-endian TIFF
        var decoded = await _decoder.LoadAsync(path, cancellationToken: Ct);
        decoded.Should().NotBeNull();
        decoded.ShouldHaveSamePixelsAs(frame);
        TestFrames.IsOpaque(decoded).Should().BeTrue();
    }

    [Fact]
    public async Task Jxr_FromAHalfFloatFrame_KeepsThePrimaries()
    {
        var frame = TestFrames.RgbaHalf(32, 32, (x, y) => (x < 16, y < 16) switch
        {
            (true, true) => (1f, 0f, 0f),
            (false, true) => (0f, 1f, 0f),
            (true, false) => (0f, 0f, 1f),
            _ => (1f, 1f, 1f),
        }, padding: 24);

        var path = await EncodeToFileAsync(frame, ImageFormat.Jxr);

        (await File.ReadAllBytesAsync(path, Ct))[..4].Should().Equal(0x49, 0x49, 0xBC, 0x01); // JPEG XR "II\xBC\x01"
        var decoded = await _decoder.LoadAsync(path, cancellationToken: Ct);
        decoded.Should().NotBeNull();
        decoded.Width.Should().Be(32);
        decoded.Height.Should().Be(32);
        TestFrames.IsOpaque(decoded).Should().BeTrue();
        TestFrames.PixelAt(decoded, 8, 8).ShouldBeCloseTo(new Rgb(255, 0, 0), 6);
        TestFrames.PixelAt(decoded, 24, 8).ShouldBeCloseTo(new Rgb(0, 255, 0), 6);
        TestFrames.PixelAt(decoded, 8, 24).ShouldBeCloseTo(new Rgb(0, 0, 255), 6);
        TestFrames.PixelAt(decoded, 24, 24).ShouldBeCloseTo(new Rgb(255, 255, 255), 6);
    }

    [Fact]
    public async Task Jxr_StoresLinearLight_SoMidTonesComeBackGammaEncoded()
    {
        // scRGB is linear: 0.2140 is sRGB 128, and HDR highlights above 1.0 clip to white in 8 bits.
        var frame = TestFrames.RgbaHalf(16, 16, (x, _) => x < 8 ? (0.2140f, 0.2140f, 0.2140f) : (4f, 4f, 4f));

        var path = await EncodeToFileAsync(frame, ImageFormat.Jxr);

        var decoded = await _decoder.LoadAsync(path, cancellationToken: Ct);
        decoded.Should().NotBeNull();
        TestFrames.PixelAt(decoded, 3, 8).ShouldBeCloseTo(new Rgb(128, 128, 128), 6);
        TestFrames.PixelAt(decoded, 12, 8).ShouldBeCloseTo(new Rgb(255, 255, 255), 2);
    }

    [Fact]
    public async Task Jxr_AndTheToneMappedImage_AgreeBelowTheHighlightKnee()
    {
        // The HDR sidecar and the tone-mapped 8-bit image of the same capture must show the
        // same picture wherever no highlight compression applies.
        var frame = TestFrames.RgbaHalf(8, 8, (x, y) => (x / 16f, y / 16f, 0.25f)) with { Hdr = HdrInfo.Sdr };

        var path = await EncodeToFileAsync(frame, ImageFormat.Jxr);

        var decoded = await _decoder.LoadAsync(path, cancellationToken: Ct);
        decoded.Should().NotBeNull();
        var toneMapped = HdrToneMapper.ToSdr(frame);
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                TestFrames.PixelAt(decoded, x, y).ShouldBeCloseTo(TestFrames.PixelAt(toneMapped, x, y), 6, $"at ({x}, {y})");
            }
        }
    }

    [Theory]
    [InlineData(ImageFormat.Tiff, FramePixelFormat.RgbaHalf)]
    [InlineData(ImageFormat.Jxr, FramePixelFormat.Bgra8)]
    [InlineData(ImageFormat.Png, FramePixelFormat.Bgra8)]
    [InlineData(ImageFormat.Webp, FramePixelFormat.RgbaHalf)]
    public async Task UnsupportedCombination_ThrowsEncodingFailure_AndWritesNothing(ImageFormat format, FramePixelFormat pixelFormat)
    {
        var frame = pixelFormat == FramePixelFormat.Bgra8
            ? TestFrames.Bgra(4, 4, TestFrames.Gradient)
            : TestFrames.RgbaHalf(4, 4, (_, _) => (1f, 1f, 1f));
        using var stream = new MemoryStream();

        var act = () => _encoder.EncodeAsync(frame, format, new EncoderSettings(), stream, Ct);

        (await act.Should().ThrowAsync<CaptureException>()).Which.ErrorCode.Should().Be(CaptureError.EncodingFailure);
        stream.Length.Should().Be(0);
    }

    [Fact]
    public async Task EncodeAsync_LeavesTheDestinationStreamOpen_AndAppendsAtItsPosition()
    {
        using var stream = new MemoryStream();
        stream.Write([1, 2, 3]);

        await _encoder.EncodeAsync(TestFrames.Bgra(4, 4, TestFrames.Gradient), ImageFormat.Tiff, new EncoderSettings(), stream, Ct);

        stream.CanWrite.Should().BeTrue();
        stream.ToArray()[..7].Should().Equal(1, 2, 3, 0x49, 0x49, 0x2A, 0x00);
    }

    [Fact]
    public async Task EncodeAsync_CancelledBeforeStarting_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var stream = new MemoryStream();

        var act = () => _encoder.EncodeAsync(TestFrames.Bgra(4, 4, TestFrames.Gradient), ImageFormat.Tiff, new EncoderSettings(), stream, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        stream.Length.Should().Be(0);
    }

    [Fact]
    public async Task ConcurrentEncodes_ShareTheFactorySafely()
    {
        var frame = TestFrames.Bgra(16, 9, TestFrames.Gradient, padding: 4);

        var paths = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => EncodeToFileAsync(frame, ImageFormat.Tiff), Ct)));

        foreach (var path in paths)
        {
            var decoded = await _decoder.LoadAsync(path, cancellationToken: Ct);
            decoded.Should().NotBeNull();
            decoded.ShouldHaveSamePixelsAs(frame);
        }
    }

    [Fact]
    public void Dispose_WithoutEverEncoding_DoesNotThrow()
    {
        var encoder = new WicImageEncoder();

        var act = encoder.Dispose;

        act.Should().NotThrow();
    }

    private async Task<string> EncodeToFileAsync(CapturedFrame frame, ImageFormat format)
    {
        var path = _temp.Combine($"{Guid.NewGuid():N}.{format.Extension()}");
        await using (var stream = File.Create(path))
        {
            await _encoder.EncodeAsync(frame, format, new EncoderSettings(), stream, Ct);
        }
        return path;
    }
}
