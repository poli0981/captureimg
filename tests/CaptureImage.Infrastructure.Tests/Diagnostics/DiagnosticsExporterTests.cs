// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.IO.Compression;
using System.Text.Json;
using AwesomeAssertions;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Paths;
using CaptureImage.Core.Profiles;
using CaptureImage.Infrastructure.Diagnostics;
using CaptureImage.Infrastructure.Settings;
using CaptureImage.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Diagnostics;

/// <summary>
/// <see cref="DiagnosticsExporter"/>: the bug-report zip holds a system summary, the settings
/// file and the most recent logs, with the user's profile path (the account name) redacted from
/// all of them. Data directory and destination are temp folders.
/// </summary>
public sealed class DiagnosticsExporterTests : IDisposable
{
    private static readonly string Profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private readonly TempDirectory _temp = new();
    private readonly AppPaths _paths;
    private readonly IDisplayService _displays = Substitute.For<IDisplayService>();
    private readonly IUpdateService _updates = Substitute.For<IUpdateService>();

    public DiagnosticsExporterTests()
    {
        _paths = new AppPaths(_temp.Combine("data"));
        _paths.EnsureCreated();
        IReadOnlyList<MonitorInfo> monitors =
        [
            new(0x10001, @"\\.\DISPLAY1", new PixelRect(0, 0, 3840, 2160), new PixelRect(0, 0, 3840, 2100), 144, true, new HdrInfo(true, 240, 1000)),
            new(0x10002, @"\\.\DISPLAY2", new PixelRect(3840, 0, 1920, 1080), new PixelRect(3840, 0, 1920, 1040), 96, false, HdrInfo.Sdr),
        ];
        _displays.GetMonitors().Returns(monitors);
        _updates.CurrentVersion.Returns("2.0.0-test");
        _updates.IsAvailable.Returns(false);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task ExportAsync_BundlesSystemInfoSettingsAndLogs()
    {
        File.WriteAllText(_paths.SettingsFile, """{"version":3,"culture":"vi-VN"}""");
        WriteLog("captureimage-20261018.log", "yesterday", hoursAgo: 24);
        WriteLog("captureimage-20261019.log", "today", hoursAgo: 1);
        File.WriteAllText(Path.Combine(_paths.LogsDirectory, "readme.txt"), "not a log");
        var destination = _temp.Combine("diagnostics.zip");

        var result = await CreateExporter().ExportAsync(destination, Ct);

        result.Should().Be(destination);
        File.Exists(destination + ".tmp").Should().BeFalse();
        var entries = ReadZip(destination);
        entries.Keys.Should().BeEquivalentTo("system.txt", "settings.json", "logs/captureimage-20261018.log", "logs/captureimage-20261019.log");
        entries["settings.json"].Should().Be("""{"version":3,"culture":"vi-VN"}""");
        entries["logs/captureimage-20261019.log"].Should().Be("today");

        var system = entries["system.txt"];
        system.Should().Contain("CaptureImage 2.0.0-test (Velopack install: False)");
        system.Should().Contain("OS: ").And.Contain(".NET: ").And.Contain("Processors: ").And.Contain("Graphics adapters:");
        system.Should().Contain(@"\\.\DISPLAY1: 3840x2160@(0,0), 144 DPI").And.Contain("primary, HDR on (SDR white 240 nits, peak 1000 nits)");
        system.Should().Contain(@"\\.\DISPLAY2: 1920x1080@(3840,0), 96 DPI").And.Contain("HDR off");
        _displays.Received(1).GetMonitors();
    }

    [Fact]
    public async Task ExportAsync_RedactsTheProfilePath_InSettingsAndLogs()
    {
        // A real settings.json, written by the store: inside JSON the backslashes are escaped.
        using (var store = new JsonSettingsStore(_paths, NullLogger<JsonSettingsStore>.Instance, legacySettingsFile: null))
        {
            await store.LoadAsync(Ct);
            store.Update(s => s with
            {
                Capture = s.Capture with { OutputDirectory = Path.Combine(Profile, "Pictures", "Shots") },
                Profiles = [new GameProfile { Id = "g", Name = "Game", ExecutablePath = Path.Combine(Profile, "Games", "game.exe") }],
            });
            await store.FlushAsync(Ct);
        }
        File.ReadAllText(_paths.SettingsFile).Should().Contain(JsonEncodedText.Encode(Profile).ToString(), "the test needs the escaped form on disk");
        WriteLog("captureimage-20261019.log", $"Saved {Path.Combine(Profile, "Pictures", "a.png")}\nOpened {Profile.ToUpperInvariant()}\\Desktop\n", hoursAgo: 0);
        var destination = _temp.Combine("diagnostics.zip");

        await CreateExporter().ExportAsync(destination, Ct);

        var entries = ReadZip(destination);
        foreach (var (name, text) in entries)
        {
            text.Should().NotContainEquivalentOf(Profile, "{0} must not reveal the account name", name);
            text.Should().NotContainEquivalentOf(JsonEncodedText.Encode(Profile).ToString(), "{0} must not reveal the account name", name);
        }
        using (var settings = JsonDocument.Parse(entries["settings.json"]))
        {
            settings.RootElement.GetProperty("capture").GetProperty("outputDirectory").GetString().Should().Be(@"%USERPROFILE%\Pictures\Shots");
            settings.RootElement.GetProperty("profiles")[0].GetProperty("executablePath").GetString().Should().Be(@"%USERPROFILE%\Games\game.exe");
        }
        entries["logs/captureimage-20261019.log"].Should().Contain(@"Saved %USERPROFILE%\Pictures\a.png").And.Contain(@"Opened %USERPROFILE%\Desktop");
    }

    [Fact]
    public async Task ExportAsync_IncludesOnlyTheFiveMostRecentLogs()
    {
        for (var i = 0; i < 7; i++)
        {
            WriteLog($"captureimage-{i}.log", $"log {i}", hoursAgo: 7 - i);
        }
        var destination = _temp.Combine("diagnostics.zip");

        await CreateExporter().ExportAsync(destination, Ct);

        ReadZip(destination).Keys.Where(k => k.StartsWith("logs/", StringComparison.Ordinal)).Should().BeEquivalentTo(
            "logs/captureimage-2.log", "logs/captureimage-3.log", "logs/captureimage-4.log", "logs/captureimage-5.log", "logs/captureimage-6.log");
    }

    [Fact]
    public async Task ExportAsync_ReadsALogTheAppIsStillWriting()
    {
        var path = Path.Combine(_paths.LogsDirectory, "captureimage-live.log");
        // Serilog's file sink keeps the current log open for writing and shares it for reading only.
        await using var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        writer.Write("still being written"u8);
        await writer.FlushAsync(Ct);
        var destination = _temp.Combine("diagnostics.zip");

        await CreateExporter().ExportAsync(destination, Ct);

        ReadZip(destination)["logs/captureimage-live.log"].Should().Be("still being written");
    }

    [Fact]
    public async Task ExportAsync_WithoutSettingsOrLogs_StillWritesTheSystemSummary()
    {
        var exporter = new DiagnosticsExporter(new AppPaths(_temp.Combine("never-started")), _displays, _updates, NullLogger<DiagnosticsExporter>.Instance);
        var destination = _temp.Combine("diagnostics.zip");

        await exporter.ExportAsync(destination, Ct);

        ReadZip(destination).Keys.Should().Equal("system.txt");
    }

    [Fact]
    public async Task ExportAsync_ReplacesAnExistingFile()
    {
        var destination = _temp.Combine("diagnostics.zip");
        File.WriteAllText(destination, "an older export");

        await CreateExporter().ExportAsync(destination, Ct);

        ReadZip(destination).Should().ContainKey("system.txt");
    }

    [Fact]
    public async Task ExportAsync_LeftoverTempFileFromAnInterruptedExport_DoesNotBlockTheNextOne()
    {
        var destination = _temp.Combine("diagnostics.zip");
        File.WriteAllText(destination + ".tmp", "half-written zip from a crash");

        await CreateExporter().ExportAsync(destination, Ct);

        ReadZip(destination).Should().ContainKey("system.txt");
        File.Exists(destination + ".tmp").Should().BeFalse();
    }

    [Fact]
    public async Task ExportAsync_Cancelled_WritesNothing()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var destination = _temp.Combine("diagnostics.zip");

        var act = () => CreateExporter().ExportAsync(destination, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        File.Exists(destination).Should().BeFalse();
        File.Exists(destination + ".tmp").Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ExportAsync_BlankDestination_Throws(string destination)
    {
        var act = () => CreateExporter().ExportAsync(destination, Ct);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void SuggestFileName_IsATimestampedZipName()
    {
        CreateExporter().SuggestFileName().Should().MatchRegex(@"^CaptureImage-diagnostics-\d{8}-\d{6}\.zip$");
    }

    private DiagnosticsExporter CreateExporter() =>
        new(_paths, _displays, _updates, NullLogger<DiagnosticsExporter>.Instance);

    private void WriteLog(string name, string content, int hoursAgo)
    {
        var path = Path.Combine(_paths.LogsDirectory, name);
        File.WriteAllText(path, content);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-hoursAgo));
    }

    private static Dictionary<string, string> ReadZip(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in zip.Entries)
        {
            using var reader = new StreamReader(entry.Open());
            entries[entry.FullName] = reader.ReadToEnd();
        }
        return entries;
    }
}
