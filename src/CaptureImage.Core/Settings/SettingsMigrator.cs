// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;

namespace CaptureImage.Core.Settings;

/// <summary>
/// Brings settings from any older schema up to <see cref="AppSettings.CurrentVersion"/>, and
/// normalizes values a hand edit may have broken. Pure function — the store does the I/O.
/// </summary>
public static class SettingsMigrator
{
    /// <summary>Migrate and normalize; <paramref name="changed"/> tells the store to write the file back.</summary>
    public static AppSettings Migrate(AppSettings settings, out bool changed)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var result = settings;

        if (result.Version < 3)
        {
            result = FromV2(result);
        }

        result = Normalize(result);
        changed = !Equivalent(result, settings);
        return result;
    }

    /// <summary>
    /// v1/v2 had one capture hotkey plus a Window/Region switch. The old key keeps driving the
    /// mode the user had selected; the other modes get their defaults unless that would
    /// assign the same combination twice.
    /// </summary>
    private static AppSettings FromV2(AppSettings v2)
    {
        var legacyKey = v2.CaptureHotkey is { IsUnbound: false } key ? key : HotkeyBinding.Default;
        var legacyMode = string.Equals(v2.Capture.Mode, "Region", StringComparison.OrdinalIgnoreCase)
            ? CaptureMode.Region
            : CaptureMode.Window;

        var hotkeys = new HotkeySettings();
        foreach (var mode in Enum.GetValues<CaptureMode>())
        {
            var binding = mode == legacyMode
                ? legacyKey
                : HotkeyBinding.DefaultFor(mode) == legacyKey ? HotkeyBinding.Unbound : HotkeyBinding.DefaultFor(mode);
            hotkeys = hotkeys.With(mode, binding);
        }

        return v2 with
        {
            Version = 3,
            Hotkeys = hotkeys,
            CaptureHotkey = null,
            Capture = v2.Capture with { Mode = null, AutoSwitchOnAltTab = null },
            UI = v2.UI with { ShowLogViewer = null },
        };
    }

    private static AppSettings Normalize(AppSettings s)
    {
        var capture = s.Capture ?? new CaptureSettings();
        var ui = s.UI ?? new UiSettings();
        return s with
        {
            Version = AppSettings.CurrentVersion,
            Culture = s.Culture ?? string.Empty,
            Theme = SettingValues.NormalizeTheme(s.Theme),
            LogLevel = SettingValues.NormalizeLogLevel(s.LogLevel),
            Hotkeys = NormalizeHotkeys(s.Hotkeys ?? new HotkeySettings()),
            Profiles = s.Profiles ?? [],
            CaptureHotkey = null,
            Capture = capture with
            {
                JpegQuality = SettingValues.ClampQuality(capture.JpegQuality),
                WebpQuality = SettingValues.ClampQuality(capture.WebpQuality),
                CountdownSeconds = SettingValues.NormalizeCountdown(capture.CountdownSeconds),
                ClipboardMode = SettingValues.ParseClipboardMode(capture.ClipboardMode).ToString(),
                PngCompression = SettingValues.ParsePngCompression(capture.PngCompression).ToString(),
                HdrMode = SettingValues.IsHdrEnabled(capture.HdrMode) ? "Auto" : "Off",
                OutputDirectory = capture.OutputDirectory ?? string.Empty,
                FileNameTemplate = string.IsNullOrWhiteSpace(capture.FileNameTemplate)
                    ? Pipeline.FileNameStrategy.DefaultTemplate
                    : capture.FileNameTemplate,
                Mode = null,
                AutoSwitchOnAltTab = null,
            },
            UI = ui with { ShowLogViewer = null },
        };
    }

    /// <summary>Invalid bindings (e.g. a hand-edited reserved combination) become unbound.</summary>
    private static HotkeySettings NormalizeHotkeys(HotkeySettings hotkeys)
    {
        foreach (var mode in Enum.GetValues<CaptureMode>())
        {
            var binding = hotkeys.Get(mode);
            if (!binding.IsUnbound && !binding.IsValid())
            {
                hotkeys = hotkeys.With(mode, HotkeyBinding.Unbound);
            }
        }
        return hotkeys;
    }

    // Records with collection members compare those by reference, so compare the parts that
    // the migration can change instead of relying on record equality.
    private static bool Equivalent(AppSettings a, AppSettings b) =>
        a.Version == b.Version
        && a.Culture == b.Culture
        && a.Theme == b.Theme
        && a.LogLevel == b.LogLevel
        && a.CaptureHotkey == b.CaptureHotkey
        && Equals(a.Hotkeys, b.Hotkeys)
        && Equals(a.Capture, b.Capture)
        && Equals(a.UI, b.UI)
        && ReferenceEquals(a.Profiles, b.Profiles);
}
