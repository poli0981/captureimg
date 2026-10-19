// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Text.Json;
using AwesomeAssertions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;
using CaptureImage.Core.Pipeline;
using CaptureImage.Core.Profiles;
using CaptureImage.Core.Settings;
using CaptureImage.Core.Tests.TestSupport;
using Xunit;

namespace CaptureImage.Core.Tests.Settings;

public class SettingsMigratorTests
{
    /// <summary>A real <c>settings.json</c> written by CaptureImage v1.5.1 (schema version 2).</summary>
    private const string V151SettingsJson = """{"version":2,"culture":"en-US","theme":"System","captureHotkey":{"modifiers":"Control, Shift","virtualKey":123},"capture":{"defaultFormat":"Png","jpegQuality":90,"webpQuality":85,"outputDirectory":"","fileNameTemplate":"{Game}_{yyyy}-{MM}-{dd}_{HH}-{mm}-{ss}","previewBeforeSave":false,"autoSwitchOnAltTab":false,"countdownSeconds":0,"clipboardMode":"None","mode":"Window"},"ui":{"startMinimized":false,"minimizeToTray":true,"showLogViewer":false,"soundEnabled":true,"openFolderAfterSave":false,"autoPinAfterCapture":false},"logLevel":"Information"}""";

    private const HotkeyModifiers CtrlShift = HotkeyModifiers.Control | HotkeyModifiers.Shift;

    private static readonly HotkeyBinding CtrlShiftF10 = new(CtrlShift, 0x79);
    private static readonly HotkeyBinding CtrlShiftF11 = new(CtrlShift, 0x7A);
    private static readonly HotkeyBinding CtrlShiftF12 = new(CtrlShift, 0x7B);
    private static readonly HotkeyBinding AltF9 = new(HotkeyModifiers.Alt, 0x78);

    // ---- v1.5.1 golden file -------------------------------------------------------------

    [Fact]
    public void GoldenV151File_MapsOntoTheModelBeforeMigration()
    {
        var v2 = SettingsJson.Deserialize(V151SettingsJson);

        v2.Version.Should().Be(2);
        v2.CaptureHotkey.Should().Be(CtrlShiftF12);
        v2.Capture.Mode.Should().Be("Window");
        v2.Capture.AutoSwitchOnAltTab.Should().BeFalse();
        v2.UI.ShowLogViewer.Should().BeFalse();
        v2.ExtensionData.Should().BeNull("every v1.5.1 field has a home in the v3 model");
        v2.Capture.ExtensionData.Should().BeNull();
        v2.UI.ExtensionData.Should().BeNull();
    }

    [Fact]
    public void GoldenV151File_MigratesToV3()
    {
        var migrated = SettingsMigrator.Migrate(SettingsJson.Deserialize(V151SettingsJson), out var changed);

        changed.Should().BeTrue();
        migrated.Version.Should().Be(AppSettings.CurrentVersion).And.Be(3);
        migrated.Culture.Should().Be("en-US");
        migrated.Theme.Should().Be("System");
        migrated.LogLevel.Should().Be("Information");
        migrated.Profiles.Should().BeEmpty();

        // The old hotkey keeps driving the mode that was active; the others get their defaults.
        migrated.Hotkeys.Should().Be(new HotkeySettings { Window = CtrlShiftF12, Region = CtrlShiftF11, Monitor = CtrlShiftF10 });

        // Legacy fields are cleared, kept values survive, fields new in v3 get their defaults.
        migrated.CaptureHotkey.Should().BeNull();
        migrated.Capture.Should().Be(new CaptureSettings
        {
            DefaultFormat = ImageFormat.Png,
            JpegQuality = 90,
            WebpQuality = 85,
            PngCompression = "Balanced",
            OutputDirectory = string.Empty,
            FileNameTemplate = "{Game}_{yyyy}-{MM}-{dd}_{HH}-{mm}-{ss}",
            PreviewBeforeSave = false,
            CountdownSeconds = 0,
            ClipboardMode = "None",
            IncludeCursor = false,
            IncludeWindowFrame = false,
            HdrMode = "Auto",
            SaveHdrSidecar = true,
            Mode = null,
            AutoSwitchOnAltTab = null,
        });
        migrated.UI.Should().Be(new UiSettings
        {
            StartMinimized = false,
            MinimizeToTray = true,
            SoundEnabled = true,
            OpenFolderAfterSave = false,
            AutoPinAfterCapture = false,
            ShowNotifications = true,
            ArmOnStartup = false,
            StartWithWindows = false,
            MainWindow = null,
            ShowLogViewer = null,
        });
    }

    [Fact]
    public void GoldenV151File_IsWrittenBackWithoutLegacyFields_AndReloadingIsANoOp()
    {
        var migrated = SettingsMigrator.Migrate(SettingsJson.Deserialize(V151SettingsJson), out _);
        var json = SettingsJson.Serialize(migrated);

        using (var document = JsonDocument.Parse(json))
        {
            var root = document.RootElement;
            root.GetProperty("version").GetInt32().Should().Be(3);
            root.TryGetProperty("captureHotkey", out _).Should().BeFalse();
            root.GetProperty("capture").TryGetProperty("mode", out _).Should().BeFalse();
            root.GetProperty("capture").TryGetProperty("autoSwitchOnAltTab", out _).Should().BeFalse();
            root.GetProperty("ui").TryGetProperty("showLogViewer", out _).Should().BeFalse();

            var window = root.GetProperty("hotkeys").GetProperty("window");
            window.GetProperty("modifiers").GetString().Should().Be("Control, Shift");
            window.GetProperty("virtualKey").GetUInt32().Should().Be(123);
        }

        var reloaded = SettingsJson.Deserialize(json);
        var again = SettingsMigrator.Migrate(reloaded, out var changedAgain);

        changedAgain.Should().BeFalse("a v3 file needs no rewrite");
        again.Should().Be(reloaded);
        again.Hotkeys.Should().Be(migrated.Hotkeys);
    }

    [Fact]
    public void GoldenV151File_InRegionMode_GivesTheOldHotkeyToRegion_WithoutDuplicatingIt()
    {
        var json = V151SettingsJson.Replace("\"mode\":\"Window\"", "\"mode\":\"Region\"", StringComparison.Ordinal);

        var migrated = SettingsMigrator.Migrate(SettingsJson.Deserialize(json), out var changed);

        changed.Should().BeTrue();
        migrated.Hotkeys.Region.Should().Be(CtrlShiftF12);
        migrated.Hotkeys.Window.Should().Be(HotkeyBinding.Unbound, "Ctrl+Shift+F12 is the Window default and already drives Region");
        migrated.Hotkeys.Monitor.Should().Be(CtrlShiftF10);
        migrated.Capture.Mode.Should().BeNull();
    }

    [Fact]
    public void V2File_KeepsEveryCustomizedValue()
    {
        const string json = """
            {
              "version": 2,
              "culture": "vi-VN",
              "theme": "Dark",
              "captureHotkey": { "modifiers": "Alt", "virtualKey": 120 },
              "capture": {
                "defaultFormat": "Webp",
                "jpegQuality": 70,
                "webpQuality": 60,
                "outputDirectory": "D:\\Shots",
                "fileNameTemplate": "{Process}{counter}",
                "previewBeforeSave": true,
                "autoSwitchOnAltTab": true,
                "countdownSeconds": 5,
                "clipboardMode": "CopyAndSave",
                "mode": "Window",
              },
              // Comments and trailing commas are accepted, as in production.
              "ui": {
                "startMinimized": true,
                "minimizeToTray": false,
                "showLogViewer": true,
                "soundEnabled": false,
                "openFolderAfterSave": true,
                "autoPinAfterCapture": true
              },
              "logLevel": "Debug"
            }
            """;

        var migrated = SettingsMigrator.Migrate(SettingsJson.Deserialize(json), out var changed);

        changed.Should().BeTrue();
        migrated.Culture.Should().Be("vi-VN");
        migrated.Theme.Should().Be("Dark");
        migrated.LogLevel.Should().Be("Debug");
        migrated.Hotkeys.Window.Should().Be(AltF9);
        migrated.Capture.DefaultFormat.Should().Be(ImageFormat.Webp);
        migrated.Capture.JpegQuality.Should().Be(70);
        migrated.Capture.WebpQuality.Should().Be(60);
        migrated.Capture.OutputDirectory.Should().Be(@"D:\Shots");
        migrated.Capture.FileNameTemplate.Should().Be("{Process}{counter}");
        migrated.Capture.PreviewBeforeSave.Should().BeTrue();
        migrated.Capture.CountdownSeconds.Should().Be(5);
        migrated.Capture.ClipboardMode.Should().Be("CopyAndSave");
        migrated.Capture.AutoSwitchOnAltTab.Should().BeNull();
        migrated.UI.StartMinimized.Should().BeTrue();
        migrated.UI.MinimizeToTray.Should().BeFalse();
        migrated.UI.SoundEnabled.Should().BeFalse();
        migrated.UI.OpenFolderAfterSave.Should().BeTrue();
        migrated.UI.AutoPinAfterCapture.Should().BeTrue();
        migrated.UI.ShowLogViewer.Should().BeNull();
    }

    // ---- the single v2 hotkey becomes three ---------------------------------------------

    [Theory]
    // mode          legacy key                               window            region            monitor
    [InlineData("Window", CtrlShift, 0x7Bu, "Ctrl+Shift+F12", "Ctrl+Shift+F11", "Ctrl+Shift+F10")]
    [InlineData("Window", HotkeyModifiers.Alt, 0x78u, "Alt+F9", "Ctrl+Shift+F11", "Ctrl+Shift+F10")]
    [InlineData("Region", HotkeyModifiers.Alt, 0x78u, "Ctrl+Shift+F12", "Alt+F9", "Ctrl+Shift+F10")]
    [InlineData("Region", CtrlShift, 0x7Au, "Ctrl+Shift+F12", "Ctrl+Shift+F11", "Ctrl+Shift+F10")]
    [InlineData("Window", CtrlShift, 0x7Au, "Ctrl+Shift+F11", "", "Ctrl+Shift+F10")]
    [InlineData("Window", CtrlShift, 0x79u, "Ctrl+Shift+F10", "Ctrl+Shift+F11", "")]
    [InlineData("Region", CtrlShift, 0x79u, "Ctrl+Shift+F12", "Ctrl+Shift+F10", "")]
    [InlineData("region", HotkeyModifiers.Alt, 0x78u, "Ctrl+Shift+F12", "Alt+F9", "Ctrl+Shift+F10")]
    [InlineData("Fullscreen", HotkeyModifiers.Alt, 0x78u, "Alt+F9", "Ctrl+Shift+F11", "Ctrl+Shift+F10")]
    [InlineData(null, HotkeyModifiers.Alt, 0x78u, "Alt+F9", "Ctrl+Shift+F11", "Ctrl+Shift+F10")]
    public void V2Hotkey_DrivesTheActiveMode_AndOtherModesGetNonConflictingDefaults(
        string? legacyMode,
        HotkeyModifiers modifiers,
        uint virtualKey,
        string window,
        string region,
        string monitor)
    {
        var v2 = new AppSettings
        {
            Version = 2,
            CaptureHotkey = new HotkeyBinding(modifiers, virtualKey),
            Capture = new CaptureSettings { Mode = legacyMode },
        };

        var hotkeys = SettingsMigrator.Migrate(v2, out var changed).Hotkeys;

        changed.Should().BeTrue();
        hotkeys.Window.ToString().Should().Be(window);
        hotkeys.Region.ToString().Should().Be(region);
        hotkeys.Monitor.ToString().Should().Be(monitor);
        BoundBindings(hotkeys).Should().OnlyHaveUniqueItems("one combination must never drive two modes");
    }

    [Theory]
    [InlineData(false, "Window")]
    [InlineData(true, "Window")]
    [InlineData(false, "Region")]
    [InlineData(true, "Region")]
    public void V2WithoutAHotkey_GivesTheV1DefaultToTheActiveMode(bool storedAsUnbound, string legacyMode)
    {
        var v2 = new AppSettings
        {
            Version = 2,
            CaptureHotkey = storedAsUnbound ? HotkeyBinding.Unbound : null,
            Capture = new CaptureSettings { Mode = legacyMode },
        };

        var hotkeys = SettingsMigrator.Migrate(v2, out _).Hotkeys;

        var active = legacyMode == "Region" ? CaptureMode.Region : CaptureMode.Window;
        hotkeys.Get(active).Should().Be(HotkeyBinding.Default);
        BoundBindings(hotkeys).Should().OnlyHaveUniqueItems();
        hotkeys.Monitor.Should().Be(CtrlShiftF10);
    }

    [Theory]
    [InlineData(HotkeyModifiers.None, 0x41u)]   // bare A — needs a modifier
    [InlineData(HotkeyModifiers.Win, 0x4Cu)]    // Win+L — reserved by Windows
    [InlineData(HotkeyModifiers.Control, 0x11u)] // Ctrl+Ctrl — modifier as the primary key
    public void V2WithAnInvalidHotkey_LeavesTheActiveModeUnbound(HotkeyModifiers modifiers, uint virtualKey)
    {
        var v2 = new AppSettings
        {
            Version = 2,
            CaptureHotkey = new HotkeyBinding(modifiers, virtualKey),
            Capture = new CaptureSettings { Mode = "Window" },
        };

        var hotkeys = SettingsMigrator.Migrate(v2, out _).Hotkeys;

        hotkeys.Window.Should().Be(HotkeyBinding.Unbound);
        hotkeys.Region.Should().Be(CtrlShiftF11);
        hotkeys.Monitor.Should().Be(CtrlShiftF10);
    }

    [Fact]
    public void V1File_IsMigratedLikeV2()
    {
        var json = V151SettingsJson.Replace("\"version\":2", "\"version\":1", StringComparison.Ordinal);

        var migrated = SettingsMigrator.Migrate(SettingsJson.Deserialize(json), out var changed);

        changed.Should().BeTrue();
        migrated.Version.Should().Be(3);
        migrated.Hotkeys.Window.Should().Be(CtrlShiftF12);
        migrated.CaptureHotkey.Should().BeNull();
    }

    [Fact]
    public void V2File_WithNullSections_MigratesToDefaults()
    {
        const string json = """{"version":2,"captureHotkey":{"modifiers":"Alt","virtualKey":120},"capture":null,"ui":null}""";

        var migrated = SettingsMigrator.Migrate(SettingsJson.Deserialize(json), out var changed);

        changed.Should().BeTrue();
        migrated.Hotkeys.Window.Should().Be(AltF9, "without a stored mode the old hotkey stays on Window");
        migrated.Capture.Should().Be(new CaptureSettings());
        migrated.UI.Should().Be(new UiSettings());
    }

    [Fact]
    public void UnknownFields_SurviveTheMigration()
    {
        var json = V151SettingsJson
            .Replace("\"logLevel\":\"Information\"", "\"logLevel\":\"Information\",\"fromTheFuture\":{\"answer\":42}", StringComparison.Ordinal)
            .Replace("\"mode\":\"Window\"", "\"mode\":\"Window\",\"futureCaptureOption\":true", StringComparison.Ordinal);

        var migrated = SettingsMigrator.Migrate(SettingsJson.Deserialize(json), out _);

        migrated.ExtensionData.Should().ContainKey("fromTheFuture");
        migrated.Capture.ExtensionData.Should().ContainKey("futureCaptureOption");
        var written = SettingsJson.Serialize(migrated);
        written.Should().Contain("\"fromTheFuture\"").And.Contain("\"futureCaptureOption\"");
    }

    // ---- normalization of hand-edited values ---------------------------------------------

    [Theory]
    [InlineData(int.MinValue, 1)]
    [InlineData(-20, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(55, 55)]
    [InlineData(100, 100)]
    [InlineData(101, 100)]
    [InlineData(int.MaxValue, 100)]
    public void Qualities_AreClampedTo1Through100(int stored, int expected)
    {
        var settings = new AppSettings { Capture = new CaptureSettings { JpegQuality = stored, WebpQuality = stored } };

        var migrated = SettingsMigrator.Migrate(settings, out var changed);

        migrated.Capture.JpegQuality.Should().Be(expected);
        migrated.Capture.WebpQuality.Should().Be(expected);
        changed.Should().Be(stored != expected);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 3)]
    [InlineData(5, 5)]
    [InlineData(10, 10)]
    [InlineData(1, 0)]
    [InlineData(7, 0)]
    [InlineData(-3, 0)]
    [InlineData(60, 0)]
    public void Countdown_SnapsToTheOfferedChoices(int stored, int expected)
    {
        var settings = new AppSettings { Capture = new CaptureSettings { CountdownSeconds = stored } };

        var migrated = SettingsMigrator.Migrate(settings, out var changed);

        migrated.Capture.CountdownSeconds.Should().Be(expected);
        changed.Should().Be(stored != expected);
    }

    [Theory]
    [InlineData("System", "System")]
    [InlineData("dark", "Dark")]
    [InlineData("LIGHT", "Light")]
    [InlineData("highcontrast", "HighContrast")]
    [InlineData("Purple", "System")]
    [InlineData("", "System")]
    [InlineData(null, "System")]
    public void Theme_IsCanonicalizedOrReset(string? stored, string expected)
    {
        var migrated = SettingsMigrator.Migrate(new AppSettings { Theme = stored! }, out var changed);

        migrated.Theme.Should().Be(expected);
        changed.Should().Be(stored != expected);
    }

    [Theory]
    [InlineData("Information", "Information")]
    [InlineData("debug", "Debug")]
    [InlineData("WARNING", "Warning")]
    [InlineData("error", "Error")]
    [InlineData("Verbose", "Information")]
    [InlineData("Fatal", "Information")]
    [InlineData(null, "Information")]
    public void LogLevel_IsCanonicalizedOrReset(string? stored, string expected)
    {
        var migrated = SettingsMigrator.Migrate(new AppSettings { LogLevel = stored! }, out var changed);

        migrated.LogLevel.Should().Be(expected);
        changed.Should().Be(stored != expected);
    }

    [Theory]
    [InlineData("None", "None")]
    [InlineData("copy", "Copy")]
    [InlineData("COPYANDSAVE", "CopyAndSave")]
    [InlineData("Paste", "None")]
    [InlineData("7", "None")]
    [InlineData(null, "None")]
    public void ClipboardMode_IsCanonicalizedOrReset(string? stored, string expected)
    {
        var settings = new AppSettings { Capture = new CaptureSettings { ClipboardMode = stored! } };

        SettingsMigrator.Migrate(settings, out _).Capture.ClipboardMode.Should().Be(expected);
    }

    [Theory]
    [InlineData("Fast", "Fast")]
    [InlineData("smallest", "Smallest")]
    [InlineData("Ultra", "Balanced")]
    [InlineData(null, "Balanced")]
    public void PngCompression_IsCanonicalizedOrReset(string? stored, string expected)
    {
        var settings = new AppSettings { Capture = new CaptureSettings { PngCompression = stored! } };

        SettingsMigrator.Migrate(settings, out _).Capture.PngCompression.Should().Be(expected);
    }

    [Theory]
    [InlineData("Auto", "Auto")]
    [InlineData("Off", "Off")]
    [InlineData("off", "Off")]
    [InlineData("OFF", "Off")]
    [InlineData("On", "Auto")]
    [InlineData("", "Auto")]
    [InlineData(null, "Auto")]
    public void HdrMode_IsEitherAutoOrOff(string? stored, string expected)
    {
        var settings = new AppSettings { Capture = new CaptureSettings { HdrMode = stored! } };

        SettingsMigrator.Migrate(settings, out _).Capture.HdrMode.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankFileNameTemplate_FallsBackToTheDefault(string? stored)
    {
        var settings = new AppSettings { Capture = new CaptureSettings { FileNameTemplate = stored! } };

        var migrated = SettingsMigrator.Migrate(settings, out var changed);

        migrated.Capture.FileNameTemplate.Should().Be(FileNameStrategy.DefaultTemplate);
        changed.Should().BeTrue();
    }

    [Fact]
    public void NullStrings_BecomeEmpty()
    {
        var settings = new AppSettings { Culture = null!, Capture = new CaptureSettings { OutputDirectory = null! } };

        var migrated = SettingsMigrator.Migrate(settings, out var changed);

        migrated.Culture.Should().BeEmpty();
        migrated.Capture.OutputDirectory.Should().BeEmpty();
        changed.Should().BeTrue();
    }

    [Fact]
    public void InvalidHotkeys_BecomeUnbound_ValidAndUnboundOnesAreKept()
    {
        var settings = new AppSettings
        {
            Hotkeys = new HotkeySettings
            {
                Window = new HotkeyBinding(HotkeyModifiers.Win, 0x47),       // Win+G — Game Bar
                Region = new HotkeyBinding(HotkeyModifiers.None, 0x41),      // bare A
                Monitor = new HotkeyBinding(HotkeyModifiers.Control, 0xA2),  // Ctrl+LCtrl
            },
        };

        var migrated = SettingsMigrator.Migrate(settings, out var changed);

        changed.Should().BeTrue();
        migrated.Hotkeys.Should().Be(new HotkeySettings
        {
            Window = HotkeyBinding.Unbound,
            Region = HotkeyBinding.Unbound,
            Monitor = HotkeyBinding.Unbound,
        });

        var mixed = new AppSettings
        {
            Hotkeys = new HotkeySettings { Window = AltF9, Region = HotkeyBinding.Unbound, Monitor = CtrlShiftF10 },
        };
        var kept = SettingsMigrator.Migrate(mixed, out var mixedChanged);

        mixedChanged.Should().BeFalse();
        kept.Hotkeys.Should().BeSameAs(mixed.Hotkeys);
    }

    [Fact]
    public void V3File_WithNullSections_IsRepairedWithDefaults()
    {
        const string json = """{"version":3,"culture":null,"theme":null,"logLevel":null,"hotkeys":null,"capture":null,"ui":null,"profiles":null}""";

        var migrated = SettingsMigrator.Migrate(SettingsJson.Deserialize(json), out var changed);

        changed.Should().BeTrue();
        migrated.Culture.Should().BeEmpty();
        migrated.Theme.Should().Be("System");
        migrated.LogLevel.Should().Be("Information");
        migrated.Hotkeys.Should().Be(new HotkeySettings());
        migrated.Capture.Should().Be(new CaptureSettings());
        migrated.UI.Should().Be(new UiSettings());
        migrated.Profiles.Should().BeEmpty();
    }

    // ---- v3 input -------------------------------------------------------------------------

    [Fact]
    public void Defaults_AreANoOp()
    {
        var settings = new AppSettings();

        var migrated = SettingsMigrator.Migrate(settings, out var changed);

        changed.Should().BeFalse();
        migrated.Should().Be(settings);
    }

    [Fact]
    public void CustomizedV3File_IsANoOp_AndKeepsProfilesAndUnknownFields()
    {
        const string json = """
            {
              "version": 3,
              "culture": "ja-JP",
              "theme": "HighContrast",
              "logLevel": "Warning",
              "hotkeys": {
                "window": { "modifiers": "Alt", "virtualKey": 120 },
                "region": { "modifiers": "None", "virtualKey": 0 },
                "monitor": { "modifiers": "Control, Alt", "virtualKey": 77 }
              },
              "capture": { "defaultFormat": "Tiff", "countdownSeconds": 10, "hdrMode": "Off", "clipboardMode": "Copy" },
              "ui": { "armOnStartup": true, "mainWindow": { "left": 10, "top": 20, "width": 800, "height": 600, "maximized": false } },
              "profiles": [ { "id": "p1", "name": "Elden Ring", "executableName": "eldenring.exe", "autoArm": true } ],
              "somethingNewer": [1, 2, 3]
            }
            """;
        var settings = SettingsJson.Deserialize(json);

        var migrated = SettingsMigrator.Migrate(settings, out var changed);

        changed.Should().BeFalse();
        migrated.Should().Be(settings);
        migrated.Profiles.Should().BeSameAs(settings.Profiles);
        migrated.Profiles.Should().ContainSingle().Which.Should().BeEquivalentTo(new GameProfile
        {
            Id = "p1",
            Name = "Elden Ring",
            ExecutableName = "eldenring.exe",
            AutoArm = true,
        });
        migrated.ExtensionData.Should().ContainKey("somethingNewer");
        migrated.UI.MainWindow.Should().Be(new WindowPlacement(10, 20, 800, 600, false));
    }

    [Fact]
    public void Migration_IsIdempotent()
    {
        var messy = new AppSettings
        {
            Version = 2,
            Theme = "dark",
            CaptureHotkey = AltF9,
            Capture = new CaptureSettings { Mode = "Region", JpegQuality = 0, CountdownSeconds = 4, HdrMode = "off" },
        };

        var once = SettingsMigrator.Migrate(messy, out var firstChanged);
        var twice = SettingsMigrator.Migrate(once, out var secondChanged);

        firstChanged.Should().BeTrue();
        secondChanged.Should().BeFalse();
        twice.Should().Be(once);
    }

    [Fact]
    public void NewerSchema_IsStampedWithTheCurrentVersion()
    {
        // The store keeps a backup of a newer file before it is rewritten.
        var migrated = SettingsMigrator.Migrate(new AppSettings { Version = 4 }, out var changed);

        migrated.Version.Should().Be(AppSettings.CurrentVersion);
        changed.Should().BeTrue();
    }

    [Fact]
    public void Null_Throws()
    {
        var act = () => SettingsMigrator.Migrate(null!, out _);

        act.Should().Throw<ArgumentNullException>();
    }

    private static IEnumerable<HotkeyBinding> BoundBindings(HotkeySettings hotkeys) =>
        Enum.GetValues<CaptureMode>().Select(hotkeys.Get).Where(binding => !binding.IsUnbound);
}
