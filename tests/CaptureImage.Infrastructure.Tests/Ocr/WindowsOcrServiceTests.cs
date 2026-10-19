// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CaptureImage.Infrastructure.Ocr;
using CaptureImage.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using Windows.Media.Ocr;
using Xunit;
using OcrResult = CaptureImage.Core.Abstractions.OcrResult;

namespace CaptureImage.Infrastructure.Tests.Ocr;

/// <summary>
/// <see cref="WindowsOcrService"/> with Windows.Media.Ocr on rendered text. Recognition tests
/// are skipped when no OCR language pack is installed (common on server images).
/// </summary>
public sealed class WindowsOcrServiceTests
{
    private readonly WindowsOcrService _ocr = new(NullLogger<WindowsOcrService>.Instance);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecognizeAsync_RenderedText_IsRead()
    {
        var frame = RenderText("CAPTURE IMAGE 2026", width: 900, height: 160, textSize: 64, padding: 36);

        var result = await RecognizeOrSkipAsync(frame);

        result.Status.Should().Be(OcrStatus.Success);
        result.Text.Should().ContainEquivalentOf("CAPTURE").And.Contain("2026");
    }

    [Fact]
    public async Task RecognizeAsync_FrameLargerThanTheEngineLimit_IsDownscaledAndStillRead()
    {
        var width = (int)OcrEngine.MaxImageDimension + 600;
        var frame = RenderText("SCREENSHOT 4096", width, height: 360, textSize: 180);

        var result = await RecognizeOrSkipAsync(frame);

        result.Status.Should().Be(OcrStatus.Success);
        result.Text.Should().ContainEquivalentOf("SCREENSHOT");
    }

    [Fact]
    public async Task RecognizeAsync_BlankFrame_FindsNoText()
    {
        var frame = TestFrames.Bgra(400, 200, (_, _) => new Rgb(255, 255, 255));

        var result = await RecognizeOrSkipAsync(frame);

        result.Should().Be(new OcrResult(OcrStatus.NoText, string.Empty));
    }

    [Fact]
    public async Task RecognizeAsync_HdrFrame_IsRefused()
    {
        var frame = TestFrames.RgbaHalf(8, 8, (_, _) => (1f, 1f, 1f));

        (await _ocr.RecognizeAsync(frame, "en-US", Ct)).Should().Be(OcrResult.Failed);
    }

    [Fact]
    public async Task RecognizeAsync_EmptyFrame_IsRefused()
    {
        var frame = new CapturedFrame(0, 0, 0, []);

        (await _ocr.RecognizeAsync(frame, "en-US", Ct)).Should().Be(OcrResult.Failed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a language tag!")]
    [InlineData("tlh-Piqd")]
    public async Task RecognizeAsync_MissingOrUnusableLanguage_FallsBackToTheUserLanguages(string? languageTag)
    {
        var frame = TestFrames.Bgra(64, 64, (_, _) => new Rgb(255, 255, 255));

        var result = await _ocr.RecognizeAsync(frame, languageTag, Ct);

        result.Status.Should().BeOneOf(OcrStatus.NoText, OcrStatus.EngineUnavailable);
    }

    private async Task<OcrResult> RecognizeOrSkipAsync(CapturedFrame frame)
    {
        var result = await _ocr.RecognizeAsync(frame, "en-US", Ct);
        if (result.Status == OcrStatus.EngineUnavailable)
        {
            Assert.Skip("No Windows OCR language pack is installed on this machine.");
        }
        return result;
    }

    /// <summary>Black text on white, in a BGRA frame whose rows are padded.</summary>
    private static CapturedFrame RenderText(string text, int width, int height, float textSize, int padding = 0)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var font = new SKFont(SKTypeface.FromFamilyName("Arial") ?? SKTypeface.Default, textSize);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            canvas.DrawText(text, textSize / 2, (height / 2f) + (textSize / 3), SKTextAlign.Left, font, paint);
        }

        var stride = (width * 4) + padding;
        var pixels = new byte[stride * height];
        var rendered = bitmap.Bytes;
        for (var y = 0; y < height; y++)
        {
            Buffer.BlockCopy(rendered, y * bitmap.RowBytes, pixels, y * stride, width * 4);
        }
        return new CapturedFrame(width, height, stride, pixels);
    }
}
