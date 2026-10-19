// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Errors;
using CaptureImage.Core.Library;
using CaptureImage.Core.Models;
using CaptureImage.Core.Pipeline;
using CaptureImage.Core.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaptureImage.Core.Tests.Pipeline;

public sealed class CaptureFileWriterTests : IDisposable
{
    private const string ExpectedStem = "Elden Ring_2026-10-19_07-15-00";

    private static readonly DateTimeOffset CapturedAt = new(2026, 10, 19, 7, 15, 0, TimeSpan.FromHours(2));

    private readonly TempDirectory _temp = new();
    private readonly FakeImageEncoder _encoder = new();

    public void Dispose() => _temp.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string OutputDir => _temp.Combine("Captures");

    private string ExpectedPath(string fileName) => Path.Combine(OutputDir, fileName);

    private CaptureFileWriter CreateWriter(Func<string, bool>? fileExists = null, params IImageEncoder[] encoders) =>
        new(
            encoders.Length > 0 ? encoders : [_encoder],
            new FileNameStrategy(fileExists ?? File.Exists),
            NullLogger<CaptureFileWriter>.Instance);

    private SaveRequest Request(
        ImageFormat format = ImageFormat.Png,
        CapturedFrame? frame = null,
        CapturedFrame? hdrFrame = null,
        GameTarget? target = null,
        string? directory = null) =>
        new(
            frame ?? Frames.SolidBgra(64, 36, new BgraPixel(10, 20, 30, 255)),
            hdrFrame,
            directory ?? OutputDir,
            FileNameStrategy.DefaultTemplate,
            format,
            new EncoderSettings(JpegQuality: 77),
            target ?? TestTargets.Game(),
            CaptureMode.Window,
            CapturedAt);

    private static CapturedFrame HdrFrame() =>
        Frames.SolidHalf(64, 36, new RgbaPixel(2f, 1f, 0.5f, 1f), new HdrInfo(true, 240f, 1000f));

    // ---- happy path -------------------------------------------------------------------------

    [Fact]
    public async Task SaveAsync_WritesTheEncodedImageUnderTheTemplateName()
    {
        var request = Request();

        var record = await CreateWriter().SaveAsync(request, Ct);

        var expected = ExpectedPath(ExpectedStem + ".png");
        record.FilePath.Should().Be(expected);
        (await File.ReadAllBytesAsync(expected, Ct)).Should().Equal(FakeImageEncoder.ContentFor(ImageFormat.Png));
        _temp.RelativeFiles().Should().Equal(Path.Combine("Captures", ExpectedStem + ".png"));

        var call = _encoder.Calls.Should().ContainSingle().Subject;
        call.Frame.Should().BeSameAs(request.Frame);
        call.Format.Should().Be(ImageFormat.Png);
        call.Settings.Should().BeSameAs(request.Encoder);
        call.DestinationPath.Should().Be(expected + ".tmp", "the image is encoded into a temp file and renamed into place");
    }

    [Fact]
    public async Task SaveAsync_ReturnsAnAccurateRecord()
    {
        var request = Request(format: ImageFormat.Jpeg);

        var record = await CreateWriter().SaveAsync(request, Ct);

        record.Should().Be(new CaptureRecord(
            ExpectedPath(ExpectedStem + ".jpg"),
            CapturedAt,
            64,
            36,
            ImageFormat.Jpeg,
            FakeImageEncoder.ContentFor(ImageFormat.Jpeg).Length,
            CaptureMode.Window,
            "Elden Ring",
            HdrFilePath: null));
        record.SizeBytes.Should().Be(new FileInfo(record.FilePath).Length);
    }

    [Fact]
    public async Task SaveAsync_WithoutTarget_NamesTheFileAfterTheMode()
    {
        var request = Request() with { Target = null, Mode = CaptureMode.Monitor };

        var record = await CreateWriter().SaveAsync(request, Ct);

        record.FileName.Should().Be("Monitor_2026-10-19_07-15-00.png");
        record.Game.Should().BeNull();
        record.Mode.Should().Be(CaptureMode.Monitor);
    }

    [Fact]
    public async Task SaveAsync_CreatesAMissingOutputFolder()
    {
        var nested = _temp.Combine("a", "b", "c");

        var record = await CreateWriter().SaveAsync(Request(directory: nested), Ct);

        Path.GetDirectoryName(record.FilePath).Should().Be(nested);
        File.Exists(record.FilePath).Should().BeTrue();
    }

    // ---- never overwrite ------------------------------------------------------------------

    [Fact]
    public async Task SaveAsync_ExistingFile_IsNeverOverwritten()
    {
        Directory.CreateDirectory(OutputDir);
        var existing = ExpectedPath(ExpectedStem + ".png");
        await File.WriteAllTextAsync(existing, "an earlier capture", Ct);

        var record = await CreateWriter().SaveAsync(Request(), Ct);

        record.FilePath.Should().Be(ExpectedPath(ExpectedStem + "_1.png"));
        (await File.ReadAllTextAsync(existing, Ct)).Should().Be("an earlier capture");
        (await File.ReadAllBytesAsync(record.FilePath, Ct)).Should().Equal(FakeImageEncoder.ContentFor(ImageFormat.Png));
    }

    [Fact]
    public async Task SaveAsync_NameStolenDuringTheSave_RetriesWithACounter()
    {
        // Another writer creates the file between the free-name check and the rename.
        var stolen = ExpectedPath(ExpectedStem + ".png");
        var raced = false;
        bool FileExists(string path)
        {
            if (!raced && path == stolen)
            {
                raced = true;
                Directory.CreateDirectory(OutputDir);
                File.WriteAllText(stolen, "the other writer");
                return false;
            }
            return File.Exists(path);
        }

        var record = await CreateWriter(FileExists).SaveAsync(Request(), Ct);

        record.FilePath.Should().Be(ExpectedPath(ExpectedStem + "_1.png"));
        (await File.ReadAllTextAsync(stolen, Ct)).Should().Be("the other writer");
        _encoder.Calls.Should().HaveCount(2, "the first encode lost the race and was thrown away");
        _temp.RelativeFiles().Should().BeEquivalentTo(
            new[] { Path.Combine("Captures", ExpectedStem + ".png"), Path.Combine("Captures", ExpectedStem + "_1.png") },
            "no temp file is left behind");
    }

    [Fact]
    public async Task SaveAsync_NameKeepsBeingTaken_GivesUpWithFileWriteFailure()
    {
        Directory.CreateDirectory(OutputDir);
        var taken = ExpectedPath(ExpectedStem + ".png");
        await File.WriteAllTextAsync(taken, "occupied", Ct);

        // The existence check is wrong every time, so every rename collides.
        var act = () => CreateWriter(_ => false).SaveAsync(Request(), Ct);

        (await act.Should().ThrowAsync<CaptureException>()).Which.ErrorCode.Should().Be(CaptureError.FileWriteFailure);
        (await File.ReadAllTextAsync(taken, Ct)).Should().Be("occupied");
        _encoder.Calls.Count.Should().BeGreaterThan(1);
        _temp.RelativeFiles().Should().Equal(Path.Combine("Captures", ExpectedStem + ".png"));
    }

    // ---- HDR sidecar ------------------------------------------------------------------------

    [Fact]
    public async Task SaveAsync_WithAnHdrFrame_AlsoWritesAJpegXrSidecar()
    {
        var hdr = HdrFrame();

        var record = await CreateWriter().SaveAsync(Request(hdrFrame: hdr), Ct);

        var sidecar = ExpectedPath(ExpectedStem + ".jxr");
        record.HdrFilePath.Should().Be(sidecar);
        record.HasHdr.Should().BeTrue();
        record.FilePath.Should().Be(ExpectedPath(ExpectedStem + ".png"));
        (await File.ReadAllBytesAsync(sidecar, Ct)).Should().Equal(FakeImageEncoder.ContentFor(ImageFormat.Jxr));
        (await File.ReadAllBytesAsync(record.FilePath, Ct)).Should().Equal(FakeImageEncoder.ContentFor(ImageFormat.Png));
        _encoder.Calls.Should().Contain(c => c.Format == ImageFormat.Jxr && c.Frame == hdr);
    }

    [Fact]
    public async Task SaveAsync_WithoutAnHdrFrame_WritesNoSidecar()
    {
        var record = await CreateWriter().SaveAsync(Request(hdrFrame: null), Ct);

        record.HdrFilePath.Should().BeNull();
        _encoder.Calls.Should().NotContain(c => c.Format == ImageFormat.Jxr);
    }

    [Fact]
    public async Task SaveAsync_An8BitFramePassedAsHdr_WritesNoSidecar()
    {
        var record = await CreateWriter().SaveAsync(Request(hdrFrame: Frames.SolidBgra(64, 36, new BgraPixel(1, 1, 1, 255))), Ct);

        record.HdrFilePath.Should().BeNull();
        _temp.RelativeFiles().Should().ContainSingle();
    }

    [Fact]
    public async Task SaveAsync_WithoutAJpegXrEncoder_SavesTheImageOnly()
    {
        var sdrOnly = new FakeImageEncoder((ImageFormat.Png, FramePixelFormat.Bgra8));

        var record = await CreateWriter(null, sdrOnly).SaveAsync(Request(hdrFrame: HdrFrame()), Ct);

        record.HdrFilePath.Should().BeNull();
        _temp.RelativeFiles().Should().Equal(Path.Combine("Captures", ExpectedStem + ".png"));
    }

    [Fact]
    public async Task SaveAsync_ExistingSidecar_IsNeverOverwritten()
    {
        Directory.CreateDirectory(OutputDir);
        var sidecar = ExpectedPath(ExpectedStem + ".jxr");
        await File.WriteAllTextAsync(sidecar, "an older HDR capture", Ct);

        var record = await CreateWriter().SaveAsync(Request(hdrFrame: HdrFrame()), Ct);

        record.FilePath.Should().Be(ExpectedPath(ExpectedStem + ".png"));
        record.HdrFilePath.Should().BeNull();
        (await File.ReadAllTextAsync(sidecar, Ct)).Should().Be("an older HDR capture");
    }

    [Fact]
    public async Task SaveAsync_FailingSidecar_DoesNotFailTheCapture()
    {
        // A COM / WIC failure while writing the JPEG XR file must not lose the saved PNG.
        _encoder.Midway = (call, _) => call.Format == ImageFormat.Jxr
            ? throw new InvalidOperationException("WIC encoder failed")
            : Task.CompletedTask;

        var record = await CreateWriter().SaveAsync(Request(hdrFrame: HdrFrame()), Ct);

        record.HdrFilePath.Should().BeNull();
        File.Exists(record.FilePath).Should().BeTrue();
        _temp.RelativeFiles().Should().Equal(Path.Combine("Captures", ExpectedStem + ".png"));
    }

    // ---- failures -----------------------------------------------------------------------------

    [Fact]
    public async Task SaveAsync_EncoderFailure_RemovesThePartialTempFile()
    {
        string? tempPath = null;
        _encoder.Midway = (call, _) =>
        {
            tempPath = call.DestinationPath;
            File.Exists(tempPath).Should().BeTrue("the encoder is writing into the temp file");
            throw new CaptureException(CaptureError.EncodingFailure, "encoder blew up");
        };

        var act = () => CreateWriter().SaveAsync(Request(), Ct);

        (await act.Should().ThrowAsync<CaptureException>()).Which.ErrorCode.Should().Be(CaptureError.EncodingFailure);
        tempPath.Should().EndWith(".tmp");
        File.Exists(tempPath).Should().BeFalse();
        _temp.RelativeFiles().Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_IoFailureWhileEncoding_IsReportedAsFileWriteFailure()
    {
        var ioError = new IOException("There is not enough space on the disk.");
        _encoder.Midway = (_, _) => throw ioError;

        var act = () => CreateWriter().SaveAsync(Request(), Ct);

        var thrown = (await act.Should().ThrowAsync<CaptureException>()).Which;
        thrown.ErrorCode.Should().Be(CaptureError.FileWriteFailure);
        thrown.InnerException.Should().BeSameAs(ioError);
        _temp.RelativeFiles().Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_UnexpectedEncoderException_PropagatesAndCleansUp()
    {
        _encoder.Midway = (_, _) => throw new InvalidOperationException("bug");

        var act = () => CreateWriter().SaveAsync(Request(), Ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _temp.RelativeFiles().Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_CancelledWhileEncoding_CleansUp()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        _encoder.Midway = async (_, token) =>
        {
            await cts.CancelAsync();
            token.ThrowIfCancellationRequested();
        };

        var act = () => CreateWriter().SaveAsync(Request(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _temp.RelativeFiles().Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_NoEncoderForTheFormat_FailsWithEncodingFailure_BeforeTouchingTheDisk()
    {
        var pngOnly = new FakeImageEncoder((ImageFormat.Png, FramePixelFormat.Bgra8));

        var act = () => CreateWriter(null, pngOnly).SaveAsync(Request(format: ImageFormat.Webp), Ct);

        (await act.Should().ThrowAsync<CaptureException>()).Which.ErrorCode.Should().Be(CaptureError.EncodingFailure);
        Directory.Exists(OutputDir).Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_EncoderIsChosenByFormatAndPixelFormat()
    {
        var jpegOnly = new FakeImageEncoder((ImageFormat.Jpeg, FramePixelFormat.Bgra8));
        var pngOnly = new FakeImageEncoder((ImageFormat.Png, FramePixelFormat.Bgra8));

        await CreateWriter(null, jpegOnly, pngOnly).SaveAsync(Request(format: ImageFormat.Png), Ct);

        jpegOnly.Calls.Should().BeEmpty();
        pngOnly.Calls.Should().ContainSingle();
    }

    [Fact]
    public async Task SaveAsync_OutputFolderCannotBeCreated_FailsWithFileWriteFailure()
    {
        // A file where a folder should be.
        var blocker = _temp.Combine("not-a-folder");
        await File.WriteAllTextAsync(blocker, "x", Ct);

        var act = () => CreateWriter().SaveAsync(Request(directory: Path.Combine(blocker, "Captures")), Ct);

        var thrown = (await act.Should().ThrowAsync<CaptureException>()).Which;
        thrown.ErrorCode.Should().Be(CaptureError.FileWriteFailure);
        thrown.InnerException.Should().BeAssignableTo<IOException>();
    }

    [Fact]
    public async Task SaveAsync_NullRequest_Throws()
    {
        var act = () => CreateWriter().SaveAsync(null!, Ct);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
