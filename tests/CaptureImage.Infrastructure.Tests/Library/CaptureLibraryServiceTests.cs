// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Text.Json;
using AwesomeAssertions;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Library;
using CaptureImage.Core.Models;
using CaptureImage.Core.Paths;
using CaptureImage.Core.Profiles;
using CaptureImage.Infrastructure.Imaging;
using CaptureImage.Infrastructure.Library;
using CaptureImage.Infrastructure.Settings;
using CaptureImage.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Library;

/// <summary>
/// <see cref="CaptureLibraryService"/> with a temp data directory and a temp output folder
/// (never the real Pictures folder): the JSON index, first-run seeding from the output folders,
/// pruning of files deleted outside the app, Recycle Bin deletes and the thumbnail cache.
/// </summary>
public sealed class CaptureLibraryServiceTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 19, 12, 0, 0, TimeSpan.FromHours(7));

    private readonly TempDirectory _temp = new();
    private readonly AppPaths _paths;
    private readonly string _output;
    private readonly ISettingsStore _settings = Substitute.For<ISettingsStore>();
    private readonly IShellService _shell = Substitute.For<IShellService>();

    public CaptureLibraryServiceTests()
    {
        _paths = new AppPaths(_temp.Combine("data"));
        _output = _temp.CreateDirectory("Captures");
        _settings.Current.Returns(new AppSettings { Capture = new CaptureSettings { OutputDirectory = _output } });
        _shell.MoveToRecycleBin(Arg.Any<string>()).Returns(true);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _temp.Dispose();

    // ---- Index ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_EmptyOutputFolder_ReturnsNothing_AndCreatesTheIndex()
    {
        using var library = CreateLibrary();

        var records = await library.GetAllAsync(Ct);

        records.Should().BeEmpty();
        (await File.ReadAllTextAsync(_paths.LibraryIndexFile, Ct)).Trim().Should().Be("[]");
    }

    [Fact]
    public async Task AddAsync_PersistsTheRecord_AndRaisesChanged()
    {
        var record = await SaveCaptureAsync("first.png", 64, 48, Noon);
        using (var library = CreateLibrary())
        {
            var changed = 0;
            library.Changed += (_, _) => changed++;

            await library.AddAsync(record, Ct);

            changed.Should().Be(1);
            (await library.GetAllAsync(Ct)).Should().ContainSingle().Which.Should().BeEquivalentTo(record);
        }

        using var reopened = CreateLibrary();
        (await reopened.GetAllAsync(Ct)).Should().ContainSingle().Which.Should().BeEquivalentTo(record, "the index is written to library.json");
    }

    [Fact]
    public async Task GetAllAsync_ListsTheNewestCaptureFirst()
    {
        using var library = CreateLibrary();
        var first = await SaveCaptureAsync("1.png", 10, 10, Noon);
        var second = await SaveCaptureAsync("2.png", 10, 10, Noon.AddMinutes(1));
        var third = await SaveCaptureAsync("3.png", 10, 10, Noon.AddMinutes(2));

        await library.AddAsync(first, Ct);
        await library.AddAsync(second, Ct);
        await library.AddAsync(third, Ct);

        (await library.GetAllAsync(Ct)).Select(r => r.FileName).Should().Equal("3.png", "2.png", "1.png");
    }

    [Fact]
    public async Task AddAsync_SameFileAgain_ReplacesTheOldRecord()
    {
        using var library = CreateLibrary();
        var record = await SaveCaptureAsync("shot.png", 10, 10, Noon);
        await library.AddAsync(record, Ct);
        await library.AddAsync(await SaveCaptureAsync("other.png", 10, 10, Noon.AddMinutes(1)), Ct);

        await library.AddAsync(record with { FilePath = record.FilePath.ToUpperInvariant(), Game = "Again" }, Ct);

        var records = await library.GetAllAsync(Ct);
        records.Should().HaveCount(2);
        records[0].Game.Should().Be("Again");
        records[1].FileName.Should().Be("other.png");
    }

    [Fact]
    public async Task AddAsync_KeepsAtMost2000Records_DroppingTheOldest()
    {
        var existing = Enumerable.Range(0, 2000)
            .Select(i => new CaptureRecord(Path.Combine(_output, $"old-{i}.png"), Noon.AddMinutes(-i), 1, 1, ImageFormat.Png, 1, CaptureMode.Window, null))
            .ToList();
        Directory.CreateDirectory(_paths.DataDirectory);
        await File.WriteAllTextAsync(_paths.LibraryIndexFile, JsonSerializer.Serialize(existing, SettingsJsonContext.Default.ListCaptureRecord), Ct);
        using var library = CreateLibrary();
        var newest = await SaveCaptureAsync("newest.png", 10, 10, Noon.AddDays(1));

        await library.AddAsync(newest, Ct);

        using var index = JsonDocument.Parse(await File.ReadAllTextAsync(_paths.LibraryIndexFile, Ct));
        var paths = index.RootElement.EnumerateArray().Select(r => r.GetProperty("filePath").GetString()).ToList();
        paths.Should().HaveCount(2000);
        paths[0].Should().Be(newest.FilePath);
        paths[^1].Should().Be(existing[1998].FilePath);
    }

    [Fact]
    public async Task GetAllAsync_DropsFilesDeletedOutsideTheApp_AndUpdatesTheIndex()
    {
        var kept = await SaveCaptureAsync("kept.png", 10, 10, Noon);
        var deleted = await SaveCaptureAsync("deleted.png", 10, 10, Noon.AddMinutes(1));
        using (var library = CreateLibrary())
        {
            await library.AddAsync(kept, Ct);
            await library.AddAsync(deleted, Ct);

            File.Delete(deleted.FilePath);

            (await library.GetAllAsync(Ct)).Should().ContainSingle().Which.FilePath.Should().Be(kept.FilePath);
        }

        (await File.ReadAllTextAsync(_paths.LibraryIndexFile, Ct)).Should().NotContain("deleted.png");
    }

    [Fact]
    public async Task GetAllAsync_UnreadableIndex_IsRebuiltFromTheOutputFolder()
    {
        await SaveCaptureAsync("on-disk.png", 12, 8, Noon);
        Directory.CreateDirectory(_paths.DataDirectory);
        await File.WriteAllTextAsync(_paths.LibraryIndexFile, "{ definitely not a list", Ct);
        using var library = CreateLibrary();

        var records = await library.GetAllAsync(Ct);

        records.Should().ContainSingle().Which.FileName.Should().Be("on-disk.png");
        (await File.ReadAllTextAsync(_paths.LibraryIndexFile, Ct)).Should().Contain("on-disk.png");
    }

    // ---- First-run seeding ---------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_FirstRun_IndexesImagesAlreadyInTheOutputFolders()
    {
        var profileFolder = _temp.CreateDirectory("ProfileShots");
        _settings.Current.Returns(new AppSettings
        {
            Capture = new CaptureSettings { OutputDirectory = _output },
            Profiles = [new GameProfile { Name = "Game", OutputDirectory = profileFolder }, new GameProfile { Name = "No folder" }],
        });
        var old = await WriteImageAsync(Path.Combine(_output, "old.png"), 40, 30, ImageFormat.Png, Noon.AddHours(-3));
        var jpeg = await WriteImageAsync(Path.Combine(_output, "mid.jpg"), 50, 20, ImageFormat.Jpeg, Noon.AddHours(-2));
        var hdr = await WriteImageAsync(Path.Combine(_output, "hdr.png"), 64, 48, ImageFormat.Png, Noon.AddHours(-1));
        var sidecar = Path.Combine(_output, "hdr.jxr");
        await File.WriteAllBytesAsync(sidecar, [0x49, 0x49, 0xBC, 0x01], Ct);
        var fromProfile = await WriteImageAsync(Path.Combine(profileFolder, "profile.png"), 16, 9, ImageFormat.Png, Noon.AddMinutes(-30));
        await File.WriteAllTextAsync(Path.Combine(_output, "notes.txt"), "not an image", Ct);
        await WriteImageAsync(Path.Combine(_temp.CreateDirectory("Captures", "nested"), "nested.png"), 8, 8, ImageFormat.Png, Noon);
        using var library = CreateLibrary();

        var records = await library.GetAllAsync(Ct);

        records.Select(r => r.FilePath).Should().Equal(fromProfile, hdr, jpeg, old);
        records.Select(r => (r.Width, r.Height)).Should().Equal((16, 9), (64, 48), (50, 20), (40, 30));
        records.Select(r => r.Format).Should().Equal(ImageFormat.Png, ImageFormat.Png, ImageFormat.Jpeg, ImageFormat.Png);
        records.Select(r => r.HdrFilePath).Should().Equal(null, sidecar, null, null);
        records.Should().OnlyContain(r => r.SizeBytes == new FileInfo(r.FilePath).Length && r.Mode == CaptureMode.Window && r.Game == null);
    }

    [Fact]
    public async Task GetAllAsync_FirstRun_ListsAnUnreadableImageWithZeroSize()
    {
        await File.WriteAllTextAsync(Path.Combine(_output, "broken.png"), "not really a png", Ct);
        using var library = CreateLibrary();

        var record = (await library.GetAllAsync(Ct)).Should().ContainSingle().Subject;

        record.FileName.Should().Be("broken.png");
        record.Width.Should().Be(0);
        record.Height.Should().Be(0);
    }

    [Fact]
    public async Task GetAllAsync_AfterTheFirstRun_DoesNotRescanTheOutputFolder()
    {
        using (var library = CreateLibrary())
        {
            (await library.GetAllAsync(Ct)).Should().BeEmpty();
        }
        await SaveCaptureAsync("made-by-another-tool.png", 10, 10, Noon);

        using var reopened = CreateLibrary();

        (await reopened.GetAllAsync(Ct)).Should().BeEmpty("only the first run seeds the index from the folders");
    }

    // ---- Delete ----------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_RecyclesTheImageAndItsHdrSidecar_AndForgetsTheRecord()
    {
        using var library = CreateLibrary();
        var record = await SaveCaptureAsync("hdr.png", 10, 10, Noon);
        var sidecar = Path.ChangeExtension(record.FilePath, ".jxr");
        await File.WriteAllBytesAsync(sidecar, [0x49, 0x49, 0xBC, 0x01], Ct);
        record = record with { HdrFilePath = sidecar };
        await library.AddAsync(record, Ct);
        var changed = 0;
        library.Changed += (_, _) => changed++;

        var deleted = await library.DeleteAsync(record, Ct);

        deleted.Should().BeTrue();
        _shell.Received(1).MoveToRecycleBin(record.FilePath);
        _shell.Received(1).MoveToRecycleBin(sidecar);
        changed.Should().Be(1);
        // The substitute does not delete anything, so the record must be gone from the index itself.
        (await library.GetAllAsync(Ct)).Should().BeEmpty();
        (await File.ReadAllTextAsync(_paths.LibraryIndexFile, Ct)).Should().NotContain("hdr.png");
    }

    [Fact]
    public async Task DeleteAsync_WhenTheRecycleBinRefuses_KeepsEverything()
    {
        _shell.MoveToRecycleBin(Arg.Any<string>()).Returns(false);
        using var library = CreateLibrary();
        var record = await SaveCaptureAsync("locked.png", 10, 10, Noon);
        var sidecar = Path.ChangeExtension(record.FilePath, ".jxr");
        await File.WriteAllBytesAsync(sidecar, [0x49, 0x49, 0xBC, 0x01], Ct);
        record = record with { HdrFilePath = sidecar };
        await library.AddAsync(record, Ct);
        var changed = 0;
        library.Changed += (_, _) => changed++;

        var deleted = await library.DeleteAsync(record, Ct);

        deleted.Should().BeFalse();
        _shell.DidNotReceive().MoveToRecycleBin(sidecar);
        changed.Should().Be(0);
        (await library.GetAllAsync(Ct)).Should().ContainSingle();
    }

    [Fact]
    public async Task DeleteAsync_FileAlreadyGone_JustForgetsTheRecord()
    {
        using var library = CreateLibrary();
        var record = await SaveCaptureAsync("gone.png", 10, 10, Noon);
        await library.AddAsync(record, Ct);
        File.Delete(record.FilePath);

        var deleted = await library.DeleteAsync(record, Ct);

        deleted.Should().BeTrue();
        _shell.DidNotReceive().MoveToRecycleBin(Arg.Any<string>());
        (await File.ReadAllTextAsync(_paths.LibraryIndexFile, Ct)).Should().NotContain("gone.png");
    }

    // ---- Thumbnails ------------------------------------------------------------------------

    [Fact]
    public async Task GetThumbnailAsync_WritesAJpegIntoTheCache_AndReusesIt()
    {
        using var library = CreateLibrary();
        using var decoder = new WicImageDecoder(NullLogger<WicImageDecoder>.Instance);
        var record = await SaveCaptureAsync("big.png", 300, 200, Noon);

        var thumbnail = await library.GetThumbnailAsync(record, 64, Ct);

        thumbnail.Should().NotBeNull();
        Path.GetDirectoryName(thumbnail).Should().Be(_paths.ThumbnailCacheDirectory);
        thumbnail.Should().EndWith("_64.jpg");
        (await File.ReadAllBytesAsync(thumbnail, Ct))[..3].Should().Equal(0xFF, 0xD8, 0xFF);
        var image = await decoder.LoadAsync(thumbnail, cancellationToken: Ct);
        image.Should().NotBeNull();
        image.Width.Should().Be(64);
        image.Height.Should().Be(43);
        TestFrames.PixelAt(image, 8, 8).ShouldBeCloseTo(TestFrames.Red, 16);
        Directory.GetFiles(_paths.ThumbnailCacheDirectory, "*.tmp").Should().BeEmpty();

        // A cached thumbnail is served as is, not regenerated.
        await File.WriteAllBytesAsync(thumbnail, [1, 2, 3], Ct);
        (await library.GetThumbnailAsync(record, 64, Ct)).Should().Be(thumbnail);
        (await File.ReadAllBytesAsync(thumbnail, Ct)).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task GetThumbnailAsync_IsKeyedBySizeAndByTheImageVersion()
    {
        using var library = CreateLibrary();
        var record = await SaveCaptureAsync("edited.png", 120, 80, Noon);
        var small = await library.GetThumbnailAsync(record, 48, Ct);
        var large = await library.GetThumbnailAsync(record, 96, Ct);

        // The user edits the image in another program: new size, new timestamp.
        await WriteImageAsync(record.FilePath, 90, 90, ImageFormat.Png, Noon.AddHours(1));
        var afterEdit = await library.GetThumbnailAsync(record, 48, Ct);

        new[] { small, large, afterEdit }.Should().OnlyHaveUniqueItems().And.NotContainNulls();
    }

    [Theory]
    [InlineData(5, 32)]
    [InlineData(5000, 1024)]
    public async Task GetThumbnailAsync_ClampsTheRequestedSize(int requested, int effective)
    {
        using var library = CreateLibrary();
        var record = await SaveCaptureAsync("any.png", 40, 20, Noon);

        var thumbnail = await library.GetThumbnailAsync(record, requested, Ct);

        thumbnail.Should().EndWith($"_{effective}.jpg");
    }

    [Fact]
    public async Task GetThumbnailAsync_SmallImage_IsNotUpscaled()
    {
        using var library = CreateLibrary();
        using var decoder = new WicImageDecoder(NullLogger<WicImageDecoder>.Instance);
        var record = await SaveCaptureAsync("tiny.png", 40, 20, Noon);

        var thumbnail = await library.GetThumbnailAsync(record, 256, Ct);

        thumbnail.Should().NotBeNull();
        var image = await decoder.LoadAsync(thumbnail, cancellationToken: Ct);
        image.Should().NotBeNull();
        image.Width.Should().Be(40);
        image.Height.Should().Be(20);
    }

    [Fact]
    public async Task GetThumbnailAsync_MissingOrUnreadableImage_ReturnsNull()
    {
        using var library = CreateLibrary();
        var missing = new CaptureRecord(Path.Combine(_output, "missing.png"), Noon, 1, 1, ImageFormat.Png, 1, CaptureMode.Window, null);
        var broken = missing with { FilePath = Path.Combine(_output, "broken.png") };
        await File.WriteAllTextAsync(broken.FilePath, "not a png", Ct);

        (await library.GetThumbnailAsync(missing, 64, Ct)).Should().BeNull();
        (await library.GetThumbnailAsync(broken, 64, Ct)).Should().BeNull();
    }

    // ---- Helpers ---------------------------------------------------------------------------

    private CaptureLibraryService CreateLibrary() =>
        new(_paths, _settings, _shell, NullLogger<CaptureLibraryService>.Instance);

    /// <summary>A real PNG in the output folder plus the record the capture pipeline would add.</summary>
    private async Task<CaptureRecord> SaveCaptureAsync(string name, int width, int height, DateTimeOffset capturedAt)
    {
        var path = await WriteImageAsync(Path.Combine(_output, name), width, height, ImageFormat.Png, capturedAt);
        return new CaptureRecord(path, capturedAt, width, height, ImageFormat.Png, new FileInfo(path).Length, CaptureMode.Window, "Test Game");
    }

    private static async Task<string> WriteImageAsync(string path, int width, int height, ImageFormat format, DateTimeOffset lastWrite)
    {
        await using (var stream = File.Create(path))
        {
            await new SkiaImageEncoder().EncodeAsync(TestFrames.Bgra(width, height, TestFrames.Quadrants(width, height)), format, new EncoderSettings(), stream, Ct);
        }
        File.SetLastWriteTimeUtc(path, lastWrite.UtcDateTime);
        return path;
    }
}
