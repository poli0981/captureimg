// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Text.Json;
using AwesomeAssertions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;
using CaptureImage.Core.Paths;
using CaptureImage.Core.Profiles;
using CaptureImage.Infrastructure.Settings;
using CaptureImage.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Settings;

/// <summary>
/// <see cref="JsonSettingsStore"/> on real files in a temp data directory: first run, adopting
/// and migrating the v1 file, backups of unreadable and newer files, debounced and flushed
/// writes, forward compatibility, import/export validation, reset and dispose.
/// </summary>
public sealed class JsonSettingsStoreTests : IDisposable
{
    /// <summary><c>settings.json</c> exactly as CaptureImage v1.5.1 writes it (schema v2).</summary>
    private const string V151SettingsJson = """{"version":2,"culture":"en-US","theme":"System","captureHotkey":{"modifiers":"Control, Shift","virtualKey":123},"capture":{"defaultFormat":"Png","jpegQuality":90,"webpQuality":85,"outputDirectory":"","fileNameTemplate":"{Game}_{yyyy}-{MM}-{dd}_{HH}-{mm}-{ss}","previewBeforeSave":false,"autoSwitchOnAltTab":false,"countdownSeconds":0,"clipboardMode":"None","mode":"Window"},"ui":{"startMinimized":false,"minimizeToTray":true,"showLogViewer":false,"soundEnabled":true,"openFolderAfterSave":false,"autoPinAfterCapture":false},"logLevel":"Information"}""";

    private static readonly HotkeyBinding CtrlShiftF12 = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x7B);

    private readonly TempDirectory _temp = new();
    private readonly AppPaths _paths;

    public JsonSettingsStoreTests()
    {
        _paths = new AppPaths(_temp.Combine("data"));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _temp.Dispose();

    // ---- First run and the v1 file ---------------------------------------------------

    [Fact]
    public async Task LoadAsync_FirstRun_WritesDefaults()
    {
        using var store = CreateStore();
        var changed = 0;
        store.Changed += (_, _) => changed++;

        await store.LoadAsync(Ct);

        store.Current.Should().BeEquivalentTo(new AppSettings());
        changed.Should().Be(1);
        Directory.Exists(_paths.LogsDirectory).Should().BeTrue();
        var json = ReadSettingsFile();
        json.GetProperty("version").GetInt32().Should().Be(AppSettings.CurrentVersion);
        json.GetProperty("hotkeys").GetProperty("window").GetProperty("modifiers").GetString().Should().Be("Control, Shift");
        json.GetProperty("capture").GetProperty("defaultFormat").GetString().Should().Be("Png");
        AllBackups().Should().BeEmpty();
        File.Exists(_paths.SettingsFile + ".tmp").Should().BeFalse();
    }

    [Fact]
    public void SettingsFilePath_IsSettingsJsonInTheDataDirectory()
    {
        using var store = CreateStore();

        store.SettingsFilePath.Should().Be(Path.Combine(_paths.DataDirectory, "settings.json"));
    }

    [Fact]
    public async Task LoadAsync_FirstRunWithV151File_CopiesItAndMigratesItToV3()
    {
        var legacy = WriteLegacyFile(V151SettingsJson);
        var legacyWrittenAt = File.GetLastWriteTimeUtc(legacy);

        using var store = await LoadStoreAsync(legacy);

        // The v1 file is only read: v1.5.1 keeps working if the user goes back to it.
        (await File.ReadAllTextAsync(legacy, Ct)).Should().Be(V151SettingsJson);
        File.GetLastWriteTimeUtc(legacy).Should().Be(legacyWrittenAt);

        var s = store.Current;
        s.Version.Should().Be(3);
        s.Culture.Should().Be("en-US");
        s.Theme.Should().Be("System");
        s.LogLevel.Should().Be("Information");
        s.CaptureHotkey.Should().BeNull();
        s.Hotkeys.Window.Should().Be(CtrlShiftF12, "the v1 hotkey keeps driving the mode that was selected (Window)");
        s.Hotkeys.Region.Should().Be(HotkeyBinding.DefaultFor(CaptureMode.Region));
        s.Hotkeys.Monitor.Should().Be(HotkeyBinding.DefaultFor(CaptureMode.Monitor));
        s.Capture.DefaultFormat.Should().Be(ImageFormat.Png);
        s.Capture.JpegQuality.Should().Be(90);
        s.Capture.WebpQuality.Should().Be(85);
        s.Capture.OutputDirectory.Should().BeEmpty();
        s.Capture.FileNameTemplate.Should().Be("{Game}_{yyyy}-{MM}-{dd}_{HH}-{mm}-{ss}");
        s.Capture.PreviewBeforeSave.Should().BeFalse();
        s.Capture.CountdownSeconds.Should().Be(0);
        s.Capture.ClipboardMode.Should().Be("None");
        s.Capture.Mode.Should().BeNull();
        s.Capture.AutoSwitchOnAltTab.Should().BeNull();
        s.UI.StartMinimized.Should().BeFalse();
        s.UI.MinimizeToTray.Should().BeTrue();
        s.UI.SoundEnabled.Should().BeTrue();
        s.UI.OpenFolderAfterSave.Should().BeFalse();
        s.UI.AutoPinAfterCapture.Should().BeFalse();
        s.UI.ShowLogViewer.Should().BeNull();

        // The pre-migration document is kept next to the migrated one.
        (await File.ReadAllTextAsync(Backups("v2").Should().ContainSingle().Subject, Ct)).Should().Be(V151SettingsJson);

        // settings.json now holds a v3 document without the retired v2 fields.
        var json = ReadSettingsFile();
        json.GetProperty("version").GetInt32().Should().Be(3);
        json.TryGetProperty("captureHotkey", out _).Should().BeFalse();
        json.GetProperty("capture").TryGetProperty("mode", out _).Should().BeFalse();
        json.GetProperty("capture").TryGetProperty("autoSwitchOnAltTab", out _).Should().BeFalse();
        json.GetProperty("ui").TryGetProperty("showLogViewer", out _).Should().BeFalse();
        json.GetProperty("hotkeys").GetProperty("window").GetProperty("virtualKey").GetUInt32().Should().Be(0x7B);
    }

    [Fact]
    public async Task LoadAsync_V151File_GetsTheDefaultsOfSettingsItDidNotHave()
    {
        // v1.5.1 never wrote the options added in v2; they must take their v2 defaults.
        using var store = await LoadStoreAsync(WriteLegacyFile(V151SettingsJson));

        var defaults = new AppSettings();
        store.Current.UI.ShowNotifications.Should().Be(defaults.UI.ShowNotifications);
        store.Current.UI.ArmOnStartup.Should().Be(defaults.UI.ArmOnStartup);
        store.Current.UI.StartWithWindows.Should().Be(defaults.UI.StartWithWindows);
        store.Current.UI.MainWindow.Should().BeNull();
        store.Current.Capture.SaveHdrSidecar.Should().Be(defaults.Capture.SaveHdrSidecar);
        store.Current.Capture.HdrMode.Should().Be(defaults.Capture.HdrMode);
        store.Current.Capture.PngCompression.Should().Be(defaults.Capture.PngCompression);
        store.Current.Capture.IncludeCursor.Should().Be(defaults.Capture.IncludeCursor);
        store.Current.Capture.IncludeWindowFrame.Should().Be(defaults.Capture.IncludeWindowFrame);
        store.Current.Profiles.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_PartialSections_KeepTheDefaultsOfTheMissingFields()
    {
        WriteSettingsFile("""{"version":3,"capture":{"jpegQuality":75},"ui":{"startMinimized":true},"profiles":[{"name":"Game"}]}""");

        using var store = await LoadStoreAsync();

        var defaults = new AppSettings();
        store.Current.Capture.Should().BeEquivalentTo(defaults.Capture with { JpegQuality = 75 });
        store.Current.UI.Should().BeEquivalentTo(defaults.UI with { StartMinimized = true });
        var profile = store.Current.Profiles.Should().ContainSingle().Subject;
        profile.Name.Should().Be("Game");
        profile.Enabled.Should().BeTrue();
        profile.LockTarget.Should().BeTrue();
        profile.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoadAsync_FileWithoutAVersion_LoadsWithDefaults()
    {
        WriteSettingsFile("""{"culture":"vi-VN"}""");

        using var store = await LoadStoreAsync();

        store.Current.Should().BeEquivalentTo(new AppSettings { Culture = "vi-VN" });
    }

    [Fact]
    public async Task LoadAsync_V2FileInRegionMode_GivesTheOldKeyToRegionWithoutAssigningItTwice()
    {
        // Ctrl+Shift+F12 drove Region capture in v2 and is also the v3 Window default.
        WriteSettingsFile(V151SettingsJson.Replace("\"mode\":\"Window\"", "\"mode\":\"Region\"", StringComparison.Ordinal));

        using var store = await LoadStoreAsync();

        store.Current.Hotkeys.Region.Should().Be(CtrlShiftF12);
        store.Current.Hotkeys.Window.Should().Be(HotkeyBinding.Unbound);
        store.Current.Hotkeys.Monitor.Should().Be(HotkeyBinding.DefaultFor(CaptureMode.Monitor));
    }

    [Fact]
    public async Task LoadAsync_ExistingSettings_WinOverTheLegacyFile()
    {
        WriteSettingsFile("""{"version":3,"culture":"vi-VN"}""");
        var legacy = WriteLegacyFile(V151SettingsJson);

        using var store = await LoadStoreAsync(legacy);

        store.Current.Culture.Should().Be("vi-VN");
        (await File.ReadAllTextAsync(legacy, Ct)).Should().Be(V151SettingsJson);
    }

    [Fact]
    public async Task LoadAsync_LegacyFileMissing_StartsWithDefaults()
    {
        using var store = await LoadStoreAsync(_temp.Combine("not-installed", "settings.json"));

        store.Current.Should().BeEquivalentTo(new AppSettings());
        File.Exists(_paths.SettingsFile).Should().BeTrue();
    }

    // ---- Unreadable, newer and hand-edited files ---------------------------------------

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[1, 2, 3]")]
    [InlineData("""{"version":3,"capture":{"defaultFormat":"Bitmap"}}""")]
    public async Task LoadAsync_UnreadableFile_IsBackedUpAndReplacedWithDefaults(string content)
    {
        WriteSettingsFile(content);

        using var store = await LoadStoreAsync();

        store.Current.Should().BeEquivalentTo(new AppSettings());
        (await File.ReadAllTextAsync(Backups("corrupt").Should().ContainSingle().Subject, Ct)).Should().Be(content);
        ReadSettingsFile().GetProperty("version").GetInt32().Should().Be(AppSettings.CurrentVersion);
    }

    [Fact]
    public async Task LoadAsync_FileLargerThanFourMegabytes_IsTreatedAsUnreadable()
    {
        WriteSettingsFile("{\"culture\":\"vi-VN\"" + new string(' ', 4 * 1024 * 1024) + "}");

        using var store = await LoadStoreAsync();

        store.Current.Culture.Should().BeEmpty();
        Backups("corrupt").Should().ContainSingle();
    }

    [Fact]
    public async Task LoadAsync_FileFromANewerVersion_KeepsTheOriginalAsABackup()
    {
        const string future = """{"version":7,"culture":"fr-FR","theme":"Dark","teleporter":{"enabled":true}}""";
        WriteSettingsFile(future);

        using var store = await LoadStoreAsync();

        (await File.ReadAllTextAsync(Backups("v7").Should().ContainSingle().Subject, Ct)).Should().Be(future);
        store.Current.Version.Should().Be(AppSettings.CurrentVersion);
        store.Current.Culture.Should().Be("fr-FR");
        store.Current.Theme.Should().Be("Dark");
        ReadSettingsFile().GetProperty("teleporter").GetProperty("enabled").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_HandEditedFileWithCommentsAndTrailingCommas_Loads()
    {
        WriteSettingsFile("""
            {
              // edited by hand
              "version": 3,
              "culture": "ja-JP", /* block comment */
              "capture": { "jpegQuality": 75, },
            }
            """);

        using var store = await LoadStoreAsync();

        store.Current.Culture.Should().Be("ja-JP");
        store.Current.Capture.JpegQuality.Should().Be(75);
        AllBackups().Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_DuplicateKeys_LastOneWins()
    {
        WriteSettingsFile("""{"version":3,"culture":"en-US","culture":"vi-VN","capture":{"jpegQuality":10,"jpegQuality":70}}""");

        using var store = await LoadStoreAsync();

        store.Current.Culture.Should().Be("vi-VN");
        store.Current.Capture.JpegQuality.Should().Be(70);
        AllBackups().Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_OutOfRangeValues_AreNormalizedAndWrittenBack()
    {
        WriteSettingsFile("""
            {
              "version": 3, "theme": "neon", "logLevel": "verbose",
              "hotkeys": { "window": { "modifiers": "Alt", "virtualKey": 115 } },
              "capture": {
                "jpegQuality": 500, "webpQuality": -3, "countdownSeconds": 7,
                "clipboardMode": "Sometimes", "hdrMode": "off", "pngCompression": "Ultra", "fileNameTemplate": " "
              }
            }
            """);

        using var store = await LoadStoreAsync();

        var s = store.Current;
        s.Theme.Should().Be("System");
        s.LogLevel.Should().Be("Information");
        s.Hotkeys.Window.Should().Be(HotkeyBinding.Unbound, "Alt+F4 belongs to Windows");
        s.Capture.JpegQuality.Should().Be(100);
        s.Capture.WebpQuality.Should().Be(1);
        s.Capture.CountdownSeconds.Should().Be(0);
        s.Capture.ClipboardMode.Should().Be("None");
        s.Capture.HdrMode.Should().Be("Off");
        s.Capture.PngCompression.Should().Be("Balanced");
        s.Capture.FileNameTemplate.Should().Be(new CaptureSettings().FileNameTemplate);
        var capture = ReadSettingsFile().GetProperty("capture");
        capture.GetProperty("jpegQuality").GetInt32().Should().Be(100);
        capture.GetProperty("countdownSeconds").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task LoadAsync_NullSectionsInAV2File_AreReplacedWithDefaults()
    {
        WriteSettingsFile("""{"version":2,"culture":"de-DE","capture":null,"ui":null,"hotkeys":null,"profiles":null}""");

        using var store = await LoadStoreAsync();

        store.Current.Should().BeEquivalentTo(new AppSettings { Culture = "de-DE" });
    }

    [Fact]
    public async Task LoadAsync_NullProfileEntries_AreDropped()
    {
        WriteSettingsFile("""{"version":3,"profiles":[null,{"id":"p1","name":"Game"}]}""");

        using var store = await LoadStoreAsync();

        store.Current.Profiles.Should().ContainSingle().Which.Id.Should().Be("p1");
    }

    // ---- Forward compatibility ---------------------------------------------------------

    [Fact]
    public async Task UnknownFieldsAtEveryLevel_SurviveALoadAndSaveRoundTrip()
    {
        // Regression: the source-generated context once threw "The extension data property ...
        // cannot bind with a parameter in the deserialization constructor" for these documents.
        WriteSettingsFile("""
            {
              "version": 3,
              "rootFuture": { "nested": [1, "two", { "three": 3 }] },
              "hotkeys": { "window": { "modifiers": "Control, Shift", "virtualKey": 123 }, "hotkeysFuture": "keep" },
              "capture": { "jpegQuality": 90, "captureFuture": 1.5 },
              "ui": { "minimizeToTray": true, "uiFuture": false }
            }
            """);

        using var store = await LoadStoreAsync();
        store.Current.ExtensionData.Should().ContainKey("rootFuture");
        store.Current.Hotkeys.ExtensionData.Should().ContainKey("hotkeysFuture");
        store.Current.Capture.ExtensionData.Should().ContainKey("captureFuture");
        store.Current.UI.ExtensionData.Should().ContainKey("uiFuture");

        store.Update(s => s with { Culture = "de-DE" });
        await store.FlushAsync(Ct);

        var json = ReadSettingsFile();
        json.GetProperty("culture").GetString().Should().Be("de-DE");
        json.GetProperty("rootFuture").GetProperty("nested")[2].GetProperty("three").GetInt32().Should().Be(3);
        json.GetProperty("hotkeys").GetProperty("hotkeysFuture").GetString().Should().Be("keep");
        json.GetProperty("capture").GetProperty("captureFuture").GetDouble().Should().Be(1.5);
        json.GetProperty("ui").GetProperty("uiFuture").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task UnknownFields_SurviveTheV2ToV3Migration()
    {
        WriteSettingsFile(V151SettingsJson.Replace("\"logLevel\"", "\"fromTheFuture\":42,\"logLevel\"", StringComparison.Ordinal));

        using var store = await LoadStoreAsync();

        store.Current.Version.Should().Be(3);
        ReadSettingsFile().GetProperty("fromTheFuture").GetInt32().Should().Be(42);
    }

    // ---- Update, flush, debounce -------------------------------------------------------

    [Fact]
    public async Task Update_RaisesChanged_AndFlushAsyncPersistsTheChange()
    {
        using var store = await LoadStoreAsync();
        var changed = 0;
        store.Changed += (_, _) => changed++;

        store.Update(s => s with { Culture = "vi-VN", Capture = s.Capture with { DefaultFormat = ImageFormat.Webp } });
        await store.FlushAsync(Ct);

        changed.Should().Be(1);
        store.Current.Culture.Should().Be("vi-VN");
        var json = ReadSettingsFile();
        json.GetProperty("culture").GetString().Should().Be("vi-VN");
        json.GetProperty("capture").GetProperty("defaultFormat").GetString().Should().Be("Webp");
        File.Exists(_paths.SettingsFile + ".tmp").Should().BeFalse();

        using var reloaded = await LoadStoreAsync();
        reloaded.Current.Should().BeEquivalentTo(store.Current);
    }

    [Fact]
    public async Task Update_WithoutFlush_IsWrittenAfterTheDebounceDelay()
    {
        using var store = await LoadStoreAsync();
        File.Delete(_paths.SettingsFile);

        store.Update(s => s with { Theme = "Dark" });

        // Polling File.Exists opens no handle, so it can't block the store's atomic replace.
        (await Eventually.TrueAsync(() => File.Exists(_paths.SettingsFile), TimeSpan.FromSeconds(10)))
            .Should().BeTrue("the debounced write lands a few hundred milliseconds after the change");
        ReadSettingsFile().GetProperty("theme").GetString().Should().Be("Dark");
    }

    [Fact]
    public async Task FlushAsync_NothingPending_WritesNothing()
    {
        using var store = await LoadStoreAsync();
        File.Delete(_paths.SettingsFile);

        await store.FlushAsync(Ct);

        File.Exists(_paths.SettingsFile).Should().BeFalse();
    }

    [Fact]
    public async Task Update_ReturningEqualSettings_IsIgnored()
    {
        using var store = await LoadStoreAsync();
        var before = store.Current;
        var changed = 0;
        store.Changed += (_, _) => changed++;

        store.Update(s => s);
        store.Update(s => s with { });

        changed.Should().Be(0);
        store.Current.Should().BeSameAs(before);
    }

    [Fact]
    public async Task Update_MutatorReturningNull_Throws()
    {
        using var store = await LoadStoreAsync();

        var act = () => store.Update(_ => null!);

        act.Should().Throw<InvalidOperationException>();
        store.Current.Should().BeEquivalentTo(new AppSettings());
    }

    [Fact]
    public async Task ConcurrentUpdatesAndFlushes_LeaveTheFileMatchingCurrent()
    {
        using var store = await LoadStoreAsync();

        var workers = Enumerable.Range(0, 8).Select(worker => Task.Run(async () =>
        {
            for (var i = 0; i < 25; i++)
            {
                var quality = (((worker * 25) + i) % 100) + 1;
                store.Update(s => s with { Capture = s.Capture with { JpegQuality = quality } });
                await store.FlushAsync(Ct);
            }
        }, Ct));
        await Task.WhenAll(workers);
        await store.FlushAsync(Ct);

        ReadSettingsFile().GetProperty("capture").GetProperty("jpegQuality").GetInt32()
            .Should().Be(store.Current.Capture.JpegQuality);
        File.Exists(_paths.SettingsFile + ".tmp").Should().BeFalse();
    }

    // ---- Export / import ---------------------------------------------------------------

    [Fact]
    public async Task ExportAsync_ThenImportAsync_RoundTripsEverySetting()
    {
        using var source = await LoadStoreAsync();
        source.Update(s => s with
        {
            Culture = "ar-SA",
            Theme = "Dark",
            LogLevel = "Debug",
            Hotkeys = s.Hotkeys.With(CaptureMode.Region, new HotkeyBinding(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x52)),
            Capture = s.Capture with
            {
                DefaultFormat = ImageFormat.Tiff,
                JpegQuality = 77,
                CountdownSeconds = 5,
                ClipboardMode = "CopyAndSave",
                OutputDirectory = _temp.Combine("shots"),
                IncludeCursor = true,
                SaveHdrSidecar = false,
            },
            UI = s.UI with { StartMinimized = true, ArmOnStartup = true, MainWindow = new WindowPlacement(10, 20, 1280, 720, false) },
            Profiles =
            [
                new GameProfile
                {
                    Id = "elden",
                    Name = "Elden Ring",
                    ExecutableName = "eldenring.exe",
                    SteamAppId = 1245620,
                    Format = ImageFormat.Png,
                    AutoArm = true,
                },
            ],
        });
        var exportPath = _temp.Combine("exported.json");

        await source.ExportAsync(exportPath, Ct);

        File.Exists(exportPath + ".tmp").Should().BeFalse();
        var otherPaths = new AppPaths(_temp.Combine("other-machine"));
        using var target = new JsonSettingsStore(otherPaths, NullLogger<JsonSettingsStore>.Instance, legacySettingsFile: null);
        await target.LoadAsync(Ct);
        var changed = 0;
        target.Changed += (_, _) => changed++;

        await target.ImportAsync(exportPath, Ct);

        changed.Should().Be(1);
        target.Current.Should().BeEquivalentTo(source.Current);
        using var reloaded = new JsonSettingsStore(otherPaths, NullLogger<JsonSettingsStore>.Instance, legacySettingsFile: null);
        await reloaded.LoadAsync(Ct);
        reloaded.Current.Should().BeEquivalentTo(source.Current, "an import is written to disk right away");
    }

    [Fact]
    public async Task ImportAsync_V151File_IsMigrated()
    {
        using var store = await LoadStoreAsync();
        var import = WriteImportFile(V151SettingsJson);

        await store.ImportAsync(import, Ct);

        store.Current.Version.Should().Be(3);
        store.Current.Hotkeys.Window.Should().Be(CtrlShiftF12);
        store.Current.CaptureHotkey.Should().BeNull();
        ReadSettingsFile().GetProperty("version").GetInt32().Should().Be(3);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("\"just a string\"")]
    [InlineData("""{"version":99}""")]
    public async Task ImportAsync_NotAUsableSettingsDocument_ThrowsInvalidData_AndKeepsTheCurrentSettings(string content)
    {
        using var store = await LoadCustomizedStoreAsync();
        var import = WriteImportFile(content);

        await ShouldRejectImportAsync(store, import);
    }

    [Theory]
    [InlineData("""{"version":3,"capture":{"outputDirectory":"\\\\host\\share\\x.json"}}""")]
    [InlineData("""{"version":3,"capture":{"outputDirectory":"//host/share/captures"}}""")]
    [InlineData("""{"version":3,"profiles":[{"id":"p","name":"Game","outputDirectory":"\\\\host\\share"}]}""")]
    [InlineData("""{"version":2,"capture":{"outputDirectory":"\\\\host\\share\\x"}}""")]
    public async Task ImportAsync_NetworkOutputFolder_ThrowsInvalidData_AndKeepsTheCurrentSettings(string content)
    {
        // Saving screenshots to \\host\share would send them to another machine and
        // authenticate there with the user's Windows credentials.
        using var store = await LoadCustomizedStoreAsync();
        var import = WriteImportFile(content);

        await ShouldRejectImportAsync(store, import);
    }

    [Fact]
    public async Task ImportAsync_FileLargerThanFourMegabytes_ThrowsInvalidData()
    {
        using var store = await LoadCustomizedStoreAsync();
        var import = WriteImportFile("{" + new string(' ', (4 * 1024 * 1024) + 1) + "}");

        await ShouldRejectImportAsync(store, import);
    }

    [Fact]
    public async Task ImportAsync_MissingFile_ThrowsFileNotFound()
    {
        using var store = await LoadStoreAsync();

        var act = () => store.ImportAsync(_temp.Combine("missing.json"), Ct);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ImportAsync_NullSections_AreReplacedWithDefaults()
    {
        using var store = await LoadStoreAsync();
        var import = WriteImportFile("""{"version":2,"culture":"it-IT","capture":null,"ui":null,"profiles":[null]}""");

        await store.ImportAsync(import, Ct);

        store.Current.Should().BeEquivalentTo(new AppSettings { Culture = "it-IT" });
    }

    // ---- Reset and dispose -------------------------------------------------------------

    [Fact]
    public async Task ResetAsync_RestoresDefaultsButKeepsTheLanguage_AndKeepsABackup()
    {
        using var store = await LoadStoreAsync();
        store.Update(s => s with { Culture = "vi-VN", Theme = "Dark", Capture = s.Capture with { JpegQuality = 40 } });
        await store.FlushAsync(Ct);
        var changed = 0;
        store.Changed += (_, _) => changed++;

        await store.ResetAsync(Ct);

        store.Current.Should().BeEquivalentTo(new AppSettings { Culture = "vi-VN" });
        changed.Should().Be(1);
        using (var backup = JsonDocument.Parse(await File.ReadAllTextAsync(Backups("reset").Should().ContainSingle().Subject, Ct)))
        {
            backup.RootElement.GetProperty("theme").GetString().Should().Be("Dark");
            backup.RootElement.GetProperty("capture").GetProperty("jpegQuality").GetInt32().Should().Be(40);
        }
        var json = ReadSettingsFile();
        json.GetProperty("theme").GetString().Should().Be("System");
        json.GetProperty("culture").GetString().Should().Be("vi-VN");
    }

    [Fact]
    public async Task Dispose_WritesPendingChangesImmediately()
    {
        var store = await LoadStoreAsync();
        store.Update(s => s with { Theme = "HighContrast" });

        store.Dispose(); // well inside the 300 ms debounce window

        ReadSettingsFile().GetProperty("theme").GetString().Should().Be("HighContrast");
        File.Exists(_paths.SettingsFile + ".tmp").Should().BeFalse();
    }

    [Fact]
    public async Task Dispose_IsIdempotent_AndRejectsLaterUpdates()
    {
        var store = await LoadStoreAsync();

        store.Dispose();
        store.Dispose();
        var act = () => store.Update(s => s with { Theme = "Dark" });

        act.Should().Throw<ObjectDisposedException>();
    }

    // ---- Helpers -----------------------------------------------------------------------

    private JsonSettingsStore CreateStore(string? legacySettingsFile = null) =>
        new(_paths, NullLogger<JsonSettingsStore>.Instance, legacySettingsFile);

    private async Task<JsonSettingsStore> LoadStoreAsync(string? legacySettingsFile = null)
    {
        var store = CreateStore(legacySettingsFile);
        await store.LoadAsync(Ct);
        return store;
    }

    /// <summary>A loaded store whose settings differ from the defaults and are on disk.</summary>
    private async Task<JsonSettingsStore> LoadCustomizedStoreAsync()
    {
        var store = await LoadStoreAsync();
        store.Update(s => s with { Culture = "vi-VN", Capture = s.Capture with { OutputDirectory = _temp.Combine("mine") } });
        await store.FlushAsync(Ct);
        return store;
    }

    private async Task ShouldRejectImportAsync(JsonSettingsStore store, string importPath)
    {
        var before = store.Current;
        var fileBefore = await File.ReadAllTextAsync(_paths.SettingsFile, Ct);
        var changed = 0;
        store.Changed += (_, _) => changed++;

        var act = () => store.ImportAsync(importPath, Ct);

        await act.Should().ThrowAsync<InvalidDataException>();
        store.Current.Should().BeSameAs(before);
        (await File.ReadAllTextAsync(_paths.SettingsFile, Ct)).Should().Be(fileBefore);
        changed.Should().Be(0);
    }

    private void WriteSettingsFile(string json)
    {
        Directory.CreateDirectory(_paths.DataDirectory);
        File.WriteAllText(_paths.SettingsFile, json);
    }

    private string WriteLegacyFile(string json)
    {
        var path = Path.Combine(_temp.CreateDirectory("CaptureImage-v1-install"), "settings.json");
        File.WriteAllText(path, json);
        return path;
    }

    private string WriteImportFile(string json)
    {
        var path = _temp.Combine($"import-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    private JsonElement ReadSettingsFile()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(_paths.SettingsFile));
        return document.RootElement.Clone();
    }

    private string[] Backups(string tag) => Directory.GetFiles(_paths.DataDirectory, $"settings.{tag}-*.bak.json");

    private string[] AllBackups() => Directory.GetFiles(_paths.DataDirectory, "*.bak.json");
}
