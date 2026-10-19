// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Text;
using AwesomeAssertions;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Errors;
using CaptureImage.Core.Models;
using CaptureImage.Infrastructure.Imaging;
using CaptureImage.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Imaging;

/// <summary>
/// <see cref="SkiaImageEncoder"/> (PNG, JPEG, WebP): synthetic frames with padded rows are
/// encoded to files and decoded back — PNG must be lossless and opaque, the lossy formats must
/// keep colours close, and the encoder settings must have their documented effect.
/// </summary>
public sealed class SkiaImageEncoderTests : IDisposable
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly TempDirectory _temp = new();
    private readonly WicImageDecoder _decoder = new(NullLogger<WicImageDecoder>.Instance);
    private readonly SkiaImageEncoder _encoder = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _decoder.Dispose();
        _temp.Dispose();
    }

    public static TheoryData<ImageFormat, FramePixelFormat, bool> SupportMatrix()
    {
        HashSet<(ImageFormat, FramePixelFormat)> supported =
        [
            (ImageFormat.Png, FramePixelFormat.Bgra8),
            (ImageFormat.Jpeg, FramePixelFormat.Bgra8),
            (ImageFormat.Webp, FramePixelFormat.Bgra8),
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
    public void Supports_OnlyEightBitPngJpegAndWebp(ImageFormat format, FramePixelFormat pixelFormat, bool expected)
    {
        _encoder.Supports(format, pixelFormat).Should().Be(expected);
    }

    [Fact]
    public async Task Png_RoundTripsEveryPixelExactly_FromPaddedRows()
    {
        var frame = TestFrames.Bgra(7, 5, TestFrames.Gradient, padding: 12);

        var path = await EncodeToFileAsync(frame, ImageFormat.Png, new EncoderSettings());

        File.ReadAllBytes(path)[..8].Should().Equal(PngSignature);
        var decoded = await _decoder.LoadAsync(path, cancellationToken: Ct);
        decoded.Should().NotBeNull();
        decoded.ShouldHaveSamePixelsAs(frame);
        TestFrames.IsOpaque(decoded).Should().BeTrue();
    }

    [Fact]
    public async Task Png_IsWrittenWithoutAnAlphaChannel_EvenFromATranslucentFrame()
    {
        var frame = TestFrames.Bgra(8, 8, TestFrames.Gradient, alpha: 0x40);

        var path = await EncodeToFileAsync(frame, ImageFormat.Png, new EncoderSettings());

        // IHDR colour type (byte 25): 2 = truecolour without alpha.
        File.ReadAllBytes(path)[25].Should().Be(2);
        var decoded = await _decoder.LoadAsync(path, cancellationToken: Ct);
        decoded.Should().NotBeNull();
        decoded.ShouldHaveSamePixelsAs(frame);
    }

    [Fact]
    public async Task PngCompressionLevel_ChangesTheSizeButNotThePixels()
    {
        var frame = TestFrames.Bgra(128, 128, (x, y) => new Rgb((byte)x, (byte)y, (byte)((x * y) >> 6)));

        var fast = await EncodeToFileAsync(frame, ImageFormat.Png, new EncoderSettings(PngCompression: PngCompressionLevel.Fast));
        var smallest = await EncodeToFileAsync(frame, ImageFormat.Png, new EncoderSettings(PngCompression: PngCompressionLevel.Smallest));

        new FileInfo(smallest).Length.Should().BeLessThanOrEqualTo(new FileInfo(fast).Length);
        foreach (var path in new[] { fast, smallest })
        {
            var decoded = await _decoder.LoadAsync(path, cancellationToken: Ct);
            decoded.Should().NotBeNull();
            decoded.ShouldHaveSamePixelsAs(frame);
        }
    }

    [Theory]
    [InlineData(95, 8)]
    [InlineData(60, 16)]
    public async Task Jpeg_KeepsColoursClose_FromPaddedRows(int quality, int tolerance)
    {
        var frame = TestFrames.Bgra(32, 32, TestFrames.Quadrants(32, 32), padding: 20);

        var path = await EncodeToFileAsync(frame, ImageFormat.Jpeg, new EncoderSettings(JpegQuality: quality));

        File.ReadAllBytes(path)[..3].Should().Equal(0xFF, 0xD8, 0xFF);
        var decoded = await _decoder.LoadAsync(path, cancellationToken: Ct);
        decoded.Should().NotBeNull();
        decoded.Width.Should().Be(32);
        decoded.Height.Should().Be(32);
        foreach (var (x, y, color) in TestFrames.QuadrantCenters(32, 32))
        {
            TestFrames.PixelAt(decoded, x, y).ShouldBeCloseTo(color, tolerance, $"at ({x}, {y})");
        }
    }

    [Fact]
    public async Task Jpeg_HigherQualityKeepsMoreDetail()
    {
        var frame = TestFrames.Bgra(64, 64, TestFrames.Gradient);

        var high = await EncodeToFileAsync(frame, ImageFormat.Jpeg, new EncoderSettings(JpegQuality: 95));
        var low = await EncodeToFileAsync(frame, ImageFormat.Jpeg, new EncoderSettings(JpegQuality: 10));

        new FileInfo(high).Length.Should().BeGreaterThan(new FileInfo(low).Length);
    }

    [Fact]
    public async Task Webp_KeepsColoursClose_FromPaddedRows()
    {
        var frame = TestFrames.Bgra(32, 32, TestFrames.Quadrants(32, 32), padding: 8);

        var path = await EncodeToFileAsync(frame, ImageFormat.Webp, new EncoderSettings(WebpQuality: 90));

        var bytes = File.ReadAllBytes(path);
        Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("RIFF");
        Encoding.ASCII.GetString(bytes, 8, 4).Should().Be("WEBP");
        // Skia decodes WebP everywhere; WIC needs an optional Windows codec (see WicImageDecoderTests).
        using var bitmap = SKBitmap.Decode(path);
        bitmap.Should().NotBeNull();
        bitmap.Width.Should().Be(32);
        bitmap.Height.Should().Be(32);
        foreach (var (x, y, color) in TestFrames.QuadrantCenters(32, 32))
        {
            var pixel = bitmap.GetPixel(x, y);
            pixel.Alpha.Should().Be(0xFF);
            new Rgb(pixel.Red, pixel.Green, pixel.Blue).ShouldBeCloseTo(color, 12, $"at ({x}, {y})");
        }
    }

    [Theory]
    [InlineData(ImageFormat.Jpeg, 0)]
    [InlineData(ImageFormat.Jpeg, 1000)]
    [InlineData(ImageFormat.Webp, -5)]
    [InlineData(ImageFormat.Webp, 101)]
    public async Task OutOfRangeQuality_IsClampedInsteadOfFailing(ImageFormat format, int quality)
    {
        var frame = TestFrames.Bgra(16, 16, TestFrames.Gradient);

        var path = await EncodeToFileAsync(frame, format, new EncoderSettings(JpegQuality: quality, WebpQuality: quality));

        using var bitmap = SKBitmap.Decode(path);
        bitmap.Should().NotBeNull();
        bitmap.Width.Should().Be(16);
    }

    [Theory]
    [InlineData(ImageFormat.Tiff, FramePixelFormat.Bgra8)]
    [InlineData(ImageFormat.Jxr, FramePixelFormat.Bgra8)]
    [InlineData(ImageFormat.Png, FramePixelFormat.RgbaHalf)]
    [InlineData(ImageFormat.Jpeg, FramePixelFormat.RgbaHalf)]
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

    [Theory]
    [InlineData(ImageFormat.Png)]
    [InlineData(ImageFormat.Jpeg)]
    [InlineData(ImageFormat.Webp)]
    public async Task EncodeAsync_LeavesTheDestinationStreamOpen(ImageFormat format)
    {
        using var stream = new MemoryStream();

        await _encoder.EncodeAsync(TestFrames.Bgra(4, 4, TestFrames.Gradient), format, new EncoderSettings(), stream, Ct);

        stream.CanWrite.Should().BeTrue();
        stream.Length.Should().BePositive();
    }

    [Fact]
    public async Task EncodeAsync_CancelledBeforeStarting_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var stream = new MemoryStream();

        var act = () => _encoder.EncodeAsync(TestFrames.Bgra(4, 4, TestFrames.Gradient), ImageFormat.Png, new EncoderSettings(), stream, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        stream.Length.Should().Be(0);
    }

    [Fact]
    public async Task EncodeAsync_NullArguments_Throw()
    {
        var frame = TestFrames.Bgra(2, 2, TestFrames.Gradient);
        using var stream = new MemoryStream();

        await FluentActions.Awaiting(() => _encoder.EncodeAsync(null!, ImageFormat.Png, new EncoderSettings(), stream, Ct))
            .Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(() => _encoder.EncodeAsync(frame, ImageFormat.Png, null!, stream, Ct))
            .Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(() => _encoder.EncodeAsync(frame, ImageFormat.Png, new EncoderSettings(), null!, Ct))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    private async Task<string> EncodeToFileAsync(CapturedFrame frame, ImageFormat format, EncoderSettings settings)
    {
        var path = _temp.Combine($"{Guid.NewGuid():N}.{format.Extension()}");
        await using (var stream = File.Create(path))
        {
            await _encoder.EncodeAsync(frame, format, settings, stream, Ct);
        }
        return path;
    }
}
