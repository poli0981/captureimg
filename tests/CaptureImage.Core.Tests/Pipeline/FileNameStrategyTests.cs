using CaptureImage.Core.Models;
using CaptureImage.Core.Pipeline;
using FluentAssertions;
using Xunit;

namespace CaptureImage.Core.Tests.Pipeline;

public class FileNameStrategyTests
{
    private static GameTarget MakeTarget(
        string displayName = "Notepad",
        string processName = "notepad") =>
        new(
            ProcessId: 1234,
            WindowHandle: 0,
            ProcessName: processName,
            WindowTitle: displayName,
            ExecutablePath: @"C:\Windows\System32\notepad.exe",
            IconBytes: null,
            SteamInfo: null);

    private static DateTimeOffset FixedTime =>
        new(2026, 4, 15, 19, 30, 45, TimeSpan.Zero);

    [Fact]
    public void Expand_AllTokens_ProducesExpectedString()
    {
        var target = MakeTarget(displayName: "Super Mario", processName: "mario");
        var template = "{Game}_{Process}_{yyyy}-{MM}-{dd}_{HH}-{mm}-{ss}";

        var result = FileNameStrategy.ExpandTokens(template, target, FixedTime);

        result.Should().Be("Super Mario_mario_2026-04-15_19-30-45");
    }

    [Fact]
    public void Expand_UnknownToken_IsLeftVerbatim()
    {
        var target = MakeTarget();
        var result = FileNameStrategy.ExpandTokens("{Game}_{unknown}_suffix", target, FixedTime);

        result.Should().Be("Notepad_{unknown}_suffix");
    }

    [Fact]
    public void Expand_CounterToken_IsRemovedFromTemplate()
    {
        // {counter} is handled later by collision logic, not by ExpandTokens.
        var target = MakeTarget();
        var result = FileNameStrategy.ExpandTokens("{Game}{counter}", target, FixedTime);

        result.Should().Be("Notepad");
    }

    [Fact]
    public void Sanitize_IllegalCharacters_AreReplacedWithUnderscore()
    {
        var result = FileNameStrategy.Sanitize(@"My<Game>:""/\|?*Name");

        result.Should().NotContainAny("<", ">", ":", "\"", "/", "\\", "|", "?", "*");
        result.Should().Contain("My");
        result.Should().Contain("Name");
    }

    [Fact]
    public void Sanitize_WhitespaceRuns_AreCollapsed()
    {
        FileNameStrategy.Sanitize("A    B\t\tC").Should().Be("A B C");
    }

    [Fact]
    public void Sanitize_EmptyInput_ReturnsDefaultName()
    {
        FileNameStrategy.Sanitize("   ").Should().Be("capture");
        FileNameStrategy.Sanitize(string.Empty).Should().Be("capture");
    }

    [Fact]
    public void Sanitize_OnlyIllegalCharacters_ReturnsDefaultName()
    {
        FileNameStrategy.Sanitize(@"<<<>>>").Should().Be("capture");
    }

    [Fact]
    public void BuildFilePath_NoCollision_UsesSanitizedBase()
    {
        var strategy = new FileNameStrategy(_ => false);
        var target = MakeTarget(displayName: "Notepad");

        var path = strategy.BuildFilePath(
            directory: @"C:\Captures",
            template: "{Game}_{yyyy}-{MM}-{dd}_{HH}-{mm}-{ss}",
            target: target,
            captureTime: FixedTime,
            format: ImageFormat.Png);

        path.Should().Be(@"C:\Captures\Notepad_2026-04-15_19-30-45.png");
    }

    [Fact]
    public void BuildFilePath_CollisionOnce_AppendsCounter1()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\Captures\Notepad_2026-04-15_19-30-45.png",
        };
        var strategy = new FileNameStrategy(existing.Contains);
        var target = MakeTarget();

        var path = strategy.BuildFilePath(
            @"C:\Captures", "{Game}_{yyyy}-{MM}-{dd}_{HH}-{mm}-{ss}",
            target, FixedTime, ImageFormat.Png);

        path.Should().Be(@"C:\Captures\Notepad_2026-04-15_19-30-45_1.png");
    }

    [Fact]
    public void BuildFilePath_ManyCollisions_IncrementsCounterUntilFree()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\Captures\Notepad_2026-04-15_19-30-45.png",
            @"C:\Captures\Notepad_2026-04-15_19-30-45_1.png",
            @"C:\Captures\Notepad_2026-04-15_19-30-45_2.png",
            @"C:\Captures\Notepad_2026-04-15_19-30-45_3.png",
        };
        var strategy = new FileNameStrategy(existing.Contains);

        var path = strategy.BuildFilePath(
            @"C:\Captures", "{Game}_{yyyy}-{MM}-{dd}_{HH}-{mm}-{ss}",
            MakeTarget(), FixedTime, ImageFormat.Png);

        path.Should().Be(@"C:\Captures\Notepad_2026-04-15_19-30-45_4.png");
    }

    [Theory]
    [InlineData(ImageFormat.Png,  "png")]
    [InlineData(ImageFormat.Jpeg, "jpg")]
    [InlineData(ImageFormat.Webp, "webp")]
    [InlineData(ImageFormat.Tiff, "tiff")]
    public void BuildFilePath_RespectsFormatExtension(ImageFormat format, string expectedExt)
    {
        var strategy = new FileNameStrategy(_ => false);
        var path = strategy.BuildFilePath(
            @"C:\Captures", "{Game}",
            MakeTarget(), FixedTime, format);

        Path.GetExtension(path).Should().Be("." + expectedExt);
    }

    [Fact]
    public void BuildFilePath_TargetWithIllegalCharsInTitle_StillProducesValidPath()
    {
        var target = MakeTarget(displayName: "Doom/Eternal: Part <2>");
        var strategy = new FileNameStrategy(_ => false);

        var path = strategy.BuildFilePath(
            @"C:\Captures", "{Game}",
            target, FixedTime, ImageFormat.Png);

        var fileName = Path.GetFileName(path);
        fileName.Should().NotContainAny("<", ">", ":", "\"", "/", "\\", "|", "?", "*");
    }

    [Fact]
    public void ImageFormat_Extension_ReturnsLowercaseExtension()
    {
        ImageFormat.Png.Extension().Should().Be("png");
        ImageFormat.Jpeg.Extension().Should().Be("jpg");
        ImageFormat.Webp.Extension().Should().Be("webp");
        ImageFormat.Tiff.Extension().Should().Be("tiff");
    }

    [Fact]
    public void ImageFormat_MimeType_ReturnsStandardMime()
    {
        ImageFormat.Png.MimeType().Should().Be("image/png");
        ImageFormat.Jpeg.MimeType().Should().Be("image/jpeg");
        ImageFormat.Webp.MimeType().Should().Be("image/webp");
        ImageFormat.Tiff.MimeType().Should().Be("image/tiff");
    }
}
