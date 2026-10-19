// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CaptureImage.Infrastructure.Imaging;
using CaptureImage.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Imaging;

/// <summary>
/// <see cref="WicImageDecoder"/>: what the library, OCR and pinning features get back from a
/// saved file — opaque, tightly packed BGRA, optionally downscaled — and <c>null</c> (never an
/// exception) for anything that is not a readable image.
/// </summary>
public sealed class WicImageDecoderTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly WicImageDecoder _decoder = new(NullLogger<WicImageDecoder>.Instance);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _decoder.Dispose();
        _temp.Dispose();
    }

    [Fact]
    public async Task LoadAsync_Png_ReturnsATightlyPackedOpaqueBgraFrame()
    {
        var source = TestFrames.Bgra(9, 4, TestFrames.Gradient, padding: 28);
        var path = await WriteAsync(source, ImageFormat.Png);

        var frame = await _decoder.LoadAsync(path, cancellationToken: Ct);

        frame.Should().NotBeNull();
        frame.Format.Should().Be(FramePixelFormat.Bgra8);
        frame.RowStride.Should().Be(9 * 4);
        frame.Pixels.Should().HaveCount(9 * 4 * 4);
        frame.ShouldHaveSamePixelsAs(source);
        TestFrames.IsOpaque(frame).Should().BeTrue();
    }

    [Theory]
    [InlineData(200, 100, 50, 50, 25)]
    [InlineData(100, 200, 50, 25, 50)]
    [InlineData(200, 100, 200, 200, 100)]
    [InlineData(200, 100, 500, 200, 100)]
    [InlineData(200, 100, 0, 200, 100)]
    [InlineData(7, 5, 3, 3, 2)]
    [InlineData(1000, 10, 50, 50, 1)]
    public async Task LoadAsync_MaxDimension_FitsTheLongEdgeAndNeverUpscales(int width, int height, int maxDimension, int expectedWidth, int expectedHeight)
    {
        var path = await WriteAsync(TestFrames.Bgra(width, height, TestFrames.Gradient), ImageFormat.Png);

        var frame = await _decoder.LoadAsync(path, maxDimension, Ct);

        frame.Should().NotBeNull();
        frame.Width.Should().Be(expectedWidth);
        frame.Height.Should().Be(expectedHeight);
        frame.RowStride.Should().Be(expectedWidth * 4);
        frame.Pixels.Should().HaveCount(expectedWidth * expectedHeight * 4);
        TestFrames.IsOpaque(frame).Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_Downscaled_KeepsTheContent()
    {
        var path = await WriteAsync(TestFrames.Bgra(64, 64, TestFrames.Quadrants(64, 64)), ImageFormat.Png);

        var frame = await _decoder.LoadAsync(path, 16, Ct);

        frame.Should().NotBeNull();
        foreach (var (x, y, color) in TestFrames.QuadrantCenters(16, 16))
        {
            TestFrames.PixelAt(frame, x, y).ShouldBeCloseTo(color, 2, $"at ({x}, {y})");
        }
    }

    [Fact]
    public async Task LoadAsync_TranslucentImage_ComesBackOpaqueWithItsStraightColour()
    {
        var path = _temp.Combine("translucent.png");
        using (var bitmap = new SKBitmap(new SKImageInfo(4, 4, SKColorType.Bgra8888, SKAlphaType.Unpremul)))
        {
            bitmap.Erase(new SKColor(200, 100, 50, 0x40));
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            await File.WriteAllBytesAsync(path, data.ToArray(), Ct);
        }

        var frame = await _decoder.LoadAsync(path, cancellationToken: Ct);

        frame.Should().NotBeNull();
        TestFrames.IsOpaque(frame).Should().BeTrue();
        TestFrames.PixelAt(frame, 2, 2).ShouldBeCloseTo(new Rgb(200, 100, 50), 3, "the colour must not be premultiplied by the old alpha");
    }

    [Fact]
    public async Task LoadAsync_Jpeg_IsDecoded()
    {
        var path = await WriteAsync(TestFrames.Bgra(32, 32, TestFrames.Quadrants(32, 32)), ImageFormat.Jpeg);

        var frame = await _decoder.LoadAsync(path, cancellationToken: Ct);

        frame.Should().NotBeNull();
        TestFrames.PixelAt(frame, 8, 8).ShouldBeCloseTo(TestFrames.Red, 12);
    }

    [Fact]
    public async Task LoadAsync_Webp_IsDecodedWhenTheWindowsCodecIsInstalled()
    {
        var path = await WriteAsync(TestFrames.Bgra(32, 32, TestFrames.Quadrants(32, 32)), ImageFormat.Webp);

        var frame = await _decoder.LoadAsync(path, cancellationToken: Ct);

        if (frame is null)
        {
            Assert.Skip("WIC has no WebP decoder on this machine (the optional \"WebP Image Extensions\" codec is not installed).");
        }
        frame.Width.Should().Be(32);
        foreach (var (x, y, color) in TestFrames.QuadrantCenters(32, 32))
        {
            TestFrames.PixelAt(frame, x, y).ShouldBeCloseTo(color, 12, $"at ({x}, {y})");
        }
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ReturnsNull()
    {
        (await _decoder.LoadAsync(_temp.Combine("missing.png"), cancellationToken: Ct)).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LoadAsync_BlankPath_ReturnsNull(string path)
    {
        (await _decoder.LoadAsync(path, cancellationToken: Ct)).Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_Directory_ReturnsNull()
    {
        (await _decoder.LoadAsync(_temp.CreateDirectory("folder.png"), cancellationToken: Ct)).Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_NonImageFile_ReturnsNull()
    {
        var path = _temp.Combine("notes.png");
        await File.WriteAllTextAsync(path, "this is not an image", Ct);

        (await _decoder.LoadAsync(path, cancellationToken: Ct)).Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_TruncatedImage_ReturnsNull()
    {
        var complete = await WriteAsync(TestFrames.Bgra(64, 64, TestFrames.Gradient), ImageFormat.Png);
        var truncated = _temp.Combine("truncated.png");
        await File.WriteAllBytesAsync(truncated, File.ReadAllBytes(complete)[..40], Ct);

        (await _decoder.LoadAsync(truncated, cancellationToken: Ct)).Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_CancelledToken_Throws()
    {
        var path = await WriteAsync(TestFrames.Bgra(4, 4, TestFrames.Gradient), ImageFormat.Png);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => _decoder.LoadAsync(path, 0, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task LoadAsync_LeavesTheFileUnlocked()
    {
        var path = await WriteAsync(TestFrames.Bgra(4, 4, TestFrames.Gradient), ImageFormat.Png);

        (await _decoder.LoadAsync(path, cancellationToken: Ct)).Should().NotBeNull();

        var act = () => File.Delete(path);
        act.Should().NotThrow();
    }

    private async Task<string> WriteAsync(CapturedFrame frame, ImageFormat format)
    {
        var path = _temp.Combine($"{Guid.NewGuid():N}.{format.Extension()}");
        await using (var stream = File.Create(path))
        {
            await new SkiaImageEncoder().EncodeAsync(frame, format, new EncoderSettings(JpegQuality: 95, WebpQuality: 95), stream, Ct);
        }
        return path;
    }
}
