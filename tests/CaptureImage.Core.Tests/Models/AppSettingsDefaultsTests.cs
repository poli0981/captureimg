// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;
using CaptureImage.Core.Pipeline;
using Xunit;

namespace CaptureImage.Core.Tests.Models;

/// <summary>
/// Defaults are part of the schema contract: a field missing from an older file comes back
/// with these values, so they must never surprise an upgrading user.
/// </summary>
public class AppSettingsDefaultsTests
{
    [Fact]
    public void AppSettings_Defaults()
    {
        var settings = new AppSettings();

        settings.Version.Should().Be(AppSettings.CurrentVersion).And.Be(3);
        settings.Culture.Should().BeEmpty("an empty culture means: follow Windows on first run");
        settings.Theme.Should().Be("System");
        settings.LogLevel.Should().Be("Information");
        settings.Profiles.Should().BeEmpty();
        settings.CaptureHotkey.Should().BeNull("the v2 hotkey is only ever read by the migration");
        settings.ExtensionData.Should().BeNull();
    }

    [Fact]
    public void Hotkeys_DefaultToThreeDistinctValidBindings()
    {
        var hotkeys = new AppSettings().Hotkeys;

        hotkeys.Window.Should().Be(HotkeyBinding.DefaultFor(CaptureMode.Window));
        hotkeys.Region.Should().Be(HotkeyBinding.DefaultFor(CaptureMode.Region));
        hotkeys.Monitor.Should().Be(HotkeyBinding.DefaultFor(CaptureMode.Monitor));
        new[] { hotkeys.Window, hotkeys.Region, hotkeys.Monitor }.Should().OnlyHaveUniqueItems()
            .And.OnlyContain(binding => binding.Validate() == HotkeyValidationResult.Ok);
    }

    [Fact]
    public void CaptureSettings_Defaults()
    {
        var capture = new CaptureSettings();

        capture.DefaultFormat.Should().Be(ImageFormat.Png);
        capture.JpegQuality.Should().Be(90);
        capture.WebpQuality.Should().Be(85);
        capture.PngCompression.Should().Be("Balanced");
        capture.OutputDirectory.Should().BeEmpty("empty means Pictures\\CaptureImage");
        capture.FileNameTemplate.Should().Be(FileNameStrategy.DefaultTemplate);
        capture.PreviewBeforeSave.Should().BeFalse();
        capture.CountdownSeconds.Should().Be(0, "no countdown, so existing users see no behaviour change");
        capture.ClipboardMode.Should().Be("None", "file-only stays the default");
        capture.IncludeCursor.Should().BeFalse();
        capture.IncludeWindowFrame.Should().BeFalse("games are captured from their client area");
        capture.HdrMode.Should().Be("Auto");
        capture.SaveHdrSidecar.Should().BeTrue();
        capture.Mode.Should().BeNull("legacy field, never written");
        capture.AutoSwitchOnAltTab.Should().BeNull("legacy field, never written");
        capture.ExtensionData.Should().BeNull();
    }

    [Fact]
    public void UiSettings_Defaults()
    {
        var ui = new UiSettings();

        ui.StartMinimized.Should().BeFalse();
        ui.MinimizeToTray.Should().BeTrue();
        ui.SoundEnabled.Should().BeTrue();
        ui.OpenFolderAfterSave.Should().BeFalse("opening Explorer after every capture would be intrusive");
        ui.AutoPinAfterCapture.Should().BeFalse("surprise floating windows would annoy gamers");
        ui.ShowNotifications.Should().BeTrue();
        ui.ArmOnStartup.Should().BeFalse();
        ui.StartWithWindows.Should().BeFalse();
        ui.MainWindow.Should().BeNull();
        ui.ShowLogViewer.Should().BeNull("legacy field, never written");
        ui.ExtensionData.Should().BeNull();
    }

    [Fact]
    public void Defaults_AreAmongTheOfferedChoices()
    {
        var settings = new AppSettings();

        SettingValues.Themes.Should().Contain(settings.Theme);
        SettingValues.LogLevels.Should().Contain(settings.LogLevel);
        SettingValues.CountdownChoices.Should().Contain(settings.Capture.CountdownSeconds);
        ImageFormatExtensions.UserSelectable.Should().Contain(settings.Capture.DefaultFormat);
        SettingValues.ParseClipboardMode(settings.Capture.ClipboardMode).ToString().Should().Be(settings.Capture.ClipboardMode);
        SettingValues.ParsePngCompression(settings.Capture.PngCompression).ToString().Should().Be(settings.Capture.PngCompression);
    }

    [Fact]
    public void With_KeepsTheOtherDefaults()
    {
        var changed = new AppSettings() with { Theme = "Dark" };

        changed.Theme.Should().Be("Dark");
        changed.Capture.Should().Be(new CaptureSettings());
        changed.UI.Should().Be(new UiSettings());
        changed.Hotkeys.Should().Be(new HotkeySettings());
    }
}
